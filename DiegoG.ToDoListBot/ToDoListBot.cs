using DiegoG.ToDoListBot.Data;
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
using System.Collections.Concurrent;

namespace DiegoG.ToDoListBot;
public class ToDoListBot : IDisposable
{
    private readonly ConcurrentQueue<UpdateContext> updates = [];
    private readonly HashSet<Task> RunningUpdates = [];

    private readonly IServiceScope Scope;
    private readonly ILogger<ToDoListBot> Logger;
    private readonly List<Exception> MarshalledExceptions = [];

    public TelegramChatBotClient BotClient { get; }
    public ChatBotManager ChatBotManager { get; }

    public ToDoListBot(ChatBotManager manager, IOptions<BotOptions> options, IServiceProvider services, ILogger<ToDoListBot> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.BotKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.ApiHash);

        ChatBotManager = manager;

        if (options.Value.AppId is 0)
            throw new ArgumentException("AppId must be non-zero", "options.Value.AppId");

        Scope = services.CreateScope();
        var botdb = new SqliteConnection($"Data Source={Path.Combine(Program.AppData, "ToDoListBot.sqlite")}");
        BotClient = new TelegramChatBotClient(
            "ToDoListBot", 
            new Bot(options.Value.BotKey, options.Value.AppId, options.Value.ApiHash, botdb),
            manager,
            (update, manager) =>
            {
                updates.Enqueue(new TelegramUpdateContext(update, BotClient!, BotClient!.ConversationIdFactory?.Invoke(update, BotClient)));
                return Task.CompletedTask;
            }
        );
        Logger = logger;
        BotClient.BotClient.Manager.Log = SinkBotClientLog;
        BotClient.BotClient.OnError += OnError;
    }

    public static ValueTask<bool> UpdateFilter(UpdateContext u) 
        => ValueTask.FromResult(u is TelegramUpdateContext update
                                && update.Update.Type is UpdateType.Message
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

        Logger.LogDebug("Configuring ToDoListBot");

        await BotClient.SetBotDescription("ToDo List Bot", "A bot that keeps track of to-do lists", "A very simple bot that you can use to keep track of your to-do lists right here on telegram!");
        await ChatBotManager.ConfigureChatBot(BotClient);

        Logger.LogDebug("ToDoListBot Configured");

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

            int submitted = 0;
            while (updates.TryDequeue(out var update)) 
            {
                RunningUpdates.Add(ChatBotManager.SubmitUpdate(update));
                submitted++;
            }

            foreach (var update in RunningUpdates)
                if (update.IsCompleted)
                {
                    await update;
                    RunningUpdates.Remove(update);
                }

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
