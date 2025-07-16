using DiegoG.ToDoListBot.Data;
using Microsoft.EntityFrameworkCore;
using GLV.Shared.Hosting;
using Microsoft.Extensions.Hosting;
using GLV.Shared.ChatBot.EntityFramework;
using GLV.Shared.ChatBot;
using TL;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using GLV.Shared.EntityFramework;

namespace DiegoG.ToDoListBot;

public static class Program
{
    static Program()
    {
        AppData = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiegoG.ToDoListBot"));
        if (Directory.Exists(AppData) is false)
            Directory.CreateDirectory(AppData);
        AppDataFileProvider = new PhysicalFileProvider(AppData);
    }

    public static string AppData { get; } 

    public static IFileProvider AppDataFileProvider { get; }

    public static async Task Main(string[] args)
    {
        Console.WriteLine($" >!> Reading appsettings.json at {Path.Combine(AppData, "appsettings.json")}");

        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.Secret.json", true);

#if DEBUG
        builder.Configuration.AddJsonFile(AppDataFileProvider, "appsettings.json", true, false);
#else
        builder.Configuration.AddJsonFile(AppDataFileProvider, "appsettings.json", false, false);
#endif

        builder.Services.AddHostedService<ToDoListBotWorker>();

        builder.Services.ConfigureGLVDatabase<ToDoListDbContext>(builder);
        builder.Services.AddScoped<DbContext, ToDoListDbContext>();
        builder.Services.AddWorkQueue();
        builder.Services.RegisterDecoratedOptions(builder.Configuration);
        builder.Services.RegisterDecoratedWorkers();
        builder.Services.AddSingleton<ToDoListBot>();
        builder.Services.AddScoped<IConversationStore, EntityFrameworkConversationStore>();
        builder.Services.AddSingleton<IConversationStoreCache>(s => new ConversationStoreCache(TimeSpan.FromHours(5)));
        InitChatBotManager(builder.Services);

        var host = builder.Build();
        await host.Services.InitDatabase<ToDoListDbContext>();
        await host.RunAsync();
    }

    private static void InitChatBotManager(IServiceCollection collection)
    {
        var manager = ChatBotManager.CreateChatBotWithReflectedActions(
            null!,
            "ToDoListBot",
            null,
            ToDoListBot.UpdateFilter,
            collection,
            null,
            (excp, bot, services) =>
            {
                bot.SendMessage("I'm sorry, an unexpected error ocurred on my side. Can we try again?");
                services.GetService<ILogger<ChatBotManager>>()?.LogError(excp, "An unexpected exception was thrown");
                return ValueTask.CompletedTask;
            }
        );
        manager.SinkLogMessageAction = (lvl, msg, id, platform, botId, excp, services) =>
        {
            services.GetService<ILogger<ChatBotManager>>()?.Log((LogLevel)lvl, id, excp, msg);
        };
        collection.AddSingleton(manager);
    }
}