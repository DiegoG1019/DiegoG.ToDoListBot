namespace DiegoG.ToDoListBot;

public class ToDoListBotWorker(ILogger<ToDoListBotWorker> logger, ToDoListBot bot) : BackgroundService
{
    private readonly ILogger<ToDoListBotWorker> Logger = logger;
    private readonly ToDoListBot Bot = bot;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("ToDoListBotWorker running at: {time}", DateTimeOffset.Now);
        await Bot.Run(stoppingToken);
    }
}
