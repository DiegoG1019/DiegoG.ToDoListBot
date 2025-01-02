﻿using DiegoG.ToDoListBot.Data;
using DiegoG.ToDoListBot.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WTelegram.Types;
using WTelegram;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.EntityFramework;
using GLV.Shared.ChatBot.Telegram;
using Telegram.Bot.Types.Enums;
using Microsoft.Data.Sqlite;
using TL;

namespace DiegoG.ToDoListBot;
public class ToDoListBot : IDisposable
{
    private readonly IServiceScope Scope;
    private readonly ToDoListDbContext Context;
    private readonly ILogger<ToDoListBot> Logger;
    private readonly List<Exception> MarshalledExceptions = [];

    public TelegramChatBotClient BotClient { get; }
    public ChatBotManager ChatBotManager { get; }
    public TaskMarshallingTelegramBotReactor TelegramReactor { get; }

    public ToDoListBot(ChatBotManager manager, IOptions<BotOptions> options, IServiceProvider services, ILogger<ToDoListBot> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.BotKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.ApiHash);

        ChatBotManager = manager;

        if (options.Value.AppId is 0)
            throw new ArgumentException("AppId must be non-zero", "options.Value.AppId");

        Scope = services.CreateScope();
        Context = Scope.ServiceProvider.GetRequiredService<ToDoListDbContext>();
        var botdb = new SqliteConnection($"Data Source={Path.Combine(Program.AppData, "ToDoListBot.sqlite")}");
        BotClient = new TelegramChatBotClient(
            "ToDoListBot", 
            new Bot(options.Value.BotKey, options.Value.AppId, options.Value.ApiHash, botdb)
        );
        Logger = logger;
        BotClient.BotClient.Manager.Log = SinkBotClientLog;
        BotClient.BotClient.OnError += OnError;
        TelegramReactor = new(BotClient, ChatBotManager);
    }

    public static ValueTask<bool> UpdateFilter(UpdateContext u) 
        => ValueTask.FromResult(u is TelegramUpdateContext update
                                && update.Update.Type is UpdateType.Message
                                                      or UpdateType.BusinessMessage
                                                      or UpdateType.CallbackQuery
        );

    private Task OnError(Exception exception, Telegram.Bot.Polling.HandleErrorSource errorSource)
    {
        if (errorSource is Telegram.Bot.Polling.HandleErrorSource.PollingError)
            Logger.LogWarning(exception, "A polling error ocurred");
        else
        {
            Logger.LogError(exception, "An unexpected exception was thrown from the Bot Client, marshalling. Source: {source}", errorSource);
            lock (MarshalledExceptions)
                MarshalledExceptions.Add(exception);
        }

        return Task.CompletedTask;
    }

    public async Task Run(CancellationToken ct)
    {
        Logger.LogInformation("Initiating ToDoListBot");
        var configureTask = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    Logger.LogDebug("Configuring ToDoListBot");

                    await BotClient.SetBotDescription("ToDo List Bot", "A bot that keeps track of to-do lists", "A very simple bot that you can use to keep track of your to-do lists right here on telegram!");
                    await ChatBotManager.ConfigureChatBot(BotClient);

                    Logger.LogDebug("ToDoListBot Configured");
                    return;
                }
                catch(RpcException ex)
                {
                    if (ex.Message.Contains("FLOOD", StringComparison.OrdinalIgnoreCase))
                    {
                        TimeSpan delay = ex.Data["X"] as TimeSpan? ?? TimeSpan.FromSeconds(60);
                        Logger.LogWarning(ex, "Failed to configure bot, trying again in {delay}", delay);
                        await Task.Delay(delay);
                    }
                    else
                        throw;
                }
            }
        }, ct);

        while (ct.IsCancellationRequested is false)
        {
            if (MarshalledExceptions.Count > 0)
                lock (MarshalledExceptions)
                {
                    try
                    {
                        if (MarshalledExceptions.Count > 1)
                            throw new AggregateException("Multiple exceptions were unexpectedly thrown from the BotClient, see inner exceptions for more", MarshalledExceptions);

                        else if (MarshalledExceptions.Count > 0)
                            throw new ApplicationException("An exception was unexpectedly thrown from the BotClient, see inner exception for more", MarshalledExceptions[0]);
                    }
                    finally
                    {
                        MarshalledExceptions.Clear();
                    }
                }

            if (configureTask is not null && configureTask.IsCompleted)
            {
                await configureTask;
                configureTask = null;
            }

            var submitted = await TelegramReactor.SubmitAllUpdates();
            if (Logger.IsEnabled(LogLevel.Debug))
                Logger.LogDebug("Submitted {messages} updates", submitted);

            await Task.Delay(500, CancellationToken.None);
        }
    }

    private void SinkBotClientLog(int level, string message)
    {
        Logger.Log((LogLevel)level, message);
    }

    public void Dispose()
    {
        Scope.Dispose();
        GC.SuppressFinalize(this);
    }
}