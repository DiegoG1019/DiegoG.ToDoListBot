namespace DiegoG.ToDoListBot;

public class ToDoListBotWorker(ILogger<ToDoListBotWorker> logger, ToDoListBot bot) : BackgroundService
{
    private readonly ILogger<ToDoListBotWorker> Logger = logger;
    private readonly ToDoListBot Bot = bot;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("ToDoListBotWorker running at: {time}", DateTimeOffset.Now);
        while (stoppingToken.IsCancellationRequested is false)
        {
            try
            {
                Logger.LogInformation("Starting ToDoListBot");
                await Bot.Run(stoppingToken);
            }
            catch (Exception e)
            {
                Logger.LogCritical(e, "An unexpected exception was thrown whilst running ToDoListBotWorker. Trying again in 5 minutes");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        Logger.LogInformation("Ended ToDoListBotWorker exection");
    }
}
