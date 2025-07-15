using System.Diagnostics;
using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using GLV.Shared.Hosting;
using Microsoft.EntityFrameworkCore;

namespace DiegoG.ToDoListBot.Workers;

[RegisterWorker]
public class ReminderWorker(ToDoListBot bot, IServiceProvider services, ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PriorityQueue<ToDoList, DateTimeOffset> upcomingTickets = new();

        while (stoppingToken.IsCancellationRequested == false)
        {
            var dtnow = DateTimeOffset.Now;
            using (var scope = services.CreateScope().GetRequiredService(out ToDoListDbContext db))
            {
                await foreach (var list in db.ToDoLists
                                   .Where(x => string.IsNullOrWhiteSpace(x.CronExpression) == false)
                                   .AsAsyncEnumerable()
                                   .WithCancellation(stoppingToken))
                    if (list.TryGetNextReminder(out var nextReminder) && (nextReminder - dtnow) <= TimeSpan.FromSeconds(1))
                        upcomingTickets.Enqueue(list, nextReminder); 
                // nextReminder is at a later date. If it is negative, then it should have already been triggered.
                // Since cronos accounts for already-past cron triggers, we absolutely should shove this up on the queue
            }

            while (upcomingTickets.TryDequeue(out var element, out var trigger))
            {
                Debug.Assert(element is not null);
                if (dtnow < trigger)
                {
                    var delay = trigger - dtnow;
                    Debug.Assert(delay > TimeSpan.Zero);
                    await Task.Delay(delay, stoppingToken);
                }

                await SendReminder(element);
            }   
            
            await Task.Delay(1015, stoppingToken);
            // Once we have all the reminders that will trigger in the next second, we send them all at once and wait a second before querying again.
            // The result is effectively the same as these are merely reminders; and allows a simple mechanism that doesn't poll too much and keeps up to date
        }
    }

    private async Task SendReminder(ToDoList list)
    {
        try
        {
            var convoId = TelegramExtensions.GetTelegramMessageConversationId(list.ChatId, bot.BotClient.BotId);
            await bot.BotClient.SendMessage(convoId, $"Hey there! Don't forget to do the things you wrote in your list '{list.Name}'!", ToDoListKeyboards.ActionKeyboard);
        }
        catch (WTelegram.WTException e)
        {
            logger.LogError(e, "An unexpected telegram exception was thrown whilst sending a reminder");
        }
    }
}