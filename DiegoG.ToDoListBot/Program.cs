using DiegoG.ToDoListBot.Data;
using Microsoft.EntityFrameworkCore;
using GLV.Shared.Hosting;
using GLV.Shared.EntityFrameworkHosting;
using Microsoft.Extensions.Hosting;
using GLV.Shared.ChatBot.EntityFramework;
using GLV.Shared.ChatBot;
using TL;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;

namespace DiegoG.ToDoListBot;

public static class Program
{
    public static string AppData { get; } 
        = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiegoG.ToDoListBot");

    public static IFileProvider AppDataFileProvider { get; }
        = new PhysicalFileProvider(AppData);

    public static async Task Main(string[] args)
    {
        Directory.CreateDirectory(AppData);
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.Secret.json", true);

        Console.WriteLine($" >!> Reading appsettings.json at {Path.Combine(AppData, "appsettings.json")}");

#if DEBUG
        builder.Configuration.AddJsonFile(AppDataFileProvider, "appsettings.json", true, false);
#else
        builder.Configuration.AddJsonFile(AppDataFileProvider, "appsettings.json", false, false);
#endif

        builder.Services.AddHostedService<ToDoListBotWorker>();

        builder.Services.ConfigureGLVDatabase<ToDoListDbContext>(builder);
        builder.Services.AddScoped<DbContext, ToDoListDbContext>();
        builder.Services.RegisterDecoratedOptions(builder.Configuration);
        builder.Services.AddSingleton<ToDoListBot>();
        InitChatBotManager(builder.Services);

        var host = builder.Build();
        await InitDatabase(host.Services);
        await host.RunAsync();
    }

    private static void InitChatBotManager(IServiceCollection collection)
    {
        var manager = ChatBotManager.CreateChatBotWithReflectedActions(
            new ServiceDescriptor(typeof(IConversationStore), typeof(EntityFrameworkConversationStore), ServiceLifetime.Scoped),
            "ToDoListBot",
            null,
            ToDoListBot.UpdateFilter,
            collection,
            null,
            (excp, bot, services) =>
            {
                bot.RespondWithText("I'm sorry, an unexpected error ocurred on my side. Can we try again?");
                services.GetService<ILogger<ChatBotManager>>()?.LogError(excp, "An unexpected exception was thrown");
                return ValueTask.FromResult<ConversationActionEndingKind?>(ConversationActionEndingKind.Finished);
            }
        );
        manager.SinkLogMessageAction = (lvl, msg, id, excp, services) =>
        {
            services.GetService<ILogger<ChatBotManager>>()?.Log((LogLevel)lvl, id, excp, msg);
        };
        collection.AddSingleton(manager);
    }

    private static async Task InitDatabase(IServiceProvider services)
    {
        services.CreateScope().GetRequiredService<ToDoListDbContext>(out var context);
        await context.Database.EnsureCreatedAsync();
    }
}