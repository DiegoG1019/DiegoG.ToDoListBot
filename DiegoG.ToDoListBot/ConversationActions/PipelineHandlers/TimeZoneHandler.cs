using DiegoG.ToDoListBot.Data;
using DiegoG.ToDoListBot.Services;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Pipeline;
using GLV.Shared.ChatBot.Telegram;
using Microsoft.EntityFrameworkCore;

namespace DiegoG.ToDoListBot.ConversationActions.PipelineHandlers;

public class TimeZoneHandler : IChatBotPipelineHandler
{
    private static char GetSign(TimeSpan sp)
        => sp < TimeSpan.Zero ? '-' : '+';
    
    public async Task TryProcessKeyboardResponse(PipelineContext context, KeyboardResponse keyboard)
    {
        if (keyboard.MatchDataTag(ActionConstants.SetTimeZone))
        {
            var id = context.Update.ConversationId.UnpackTelegramConversationId();
            var q = await context.Services.GetRequiredService<ToDoListDbContext>().ChatConfigs
                .Where(x => x.ChatId == id)
                .Select(x => x.UtcOffset)
                .FirstOrDefaultAsync();
            
            await context.ActiveAction.SetResponseMessage($"Your current timezone is set to UTC{GetSign(q)}{q:hh':'mm}\n\nSet a new one by sending me a message with the format hour:minute (hh:mm); or send /cancel to not do that.", null, true);
            context.Context.SetStep(ActionConstants.EditTimeZoneStepSetName);
            context.Handled = true;
        }
    }

    public async Task TryProcessMessage(PipelineContext context, Message message)
    {
        if (context.Context.Step == ActionConstants.EditTimeZoneStepSetName && string.IsNullOrWhiteSpace(message.Text) is false)
        {
            if (TimeSpan.TryParse(message.Text, out var parsed))
            {
                var id = context.Update.ConversationId.UnpackTelegramConversationId();
                var q = await context.Services.GetRequiredService<ToDoListDbContext>().ChatConfigs
                    .Where(x => x.ChatId == id)
                    .ExecuteUpdateAsync(x => x.SetProperty(list => list.UtcOffset, parsed));
                
                if (q is 0)
                {
                    await context.ActiveAction.SetResponseMessage(
                        $"Sorry, it seems I couldn't modify your timezone. Could you try again?",
                        ToDoListKeyboards.ActionKeyboard
                    );
                }
                else
                {
                    await context.ActiveAction.SetResponseMessage(
                        $"Success! Changed your current timezone to {GetSign(parsed)}{parsed:hh':'mm}",
                        ToDoListKeyboards.ActionKeyboard
                    );
                }
            }
            else
                await context.ActiveAction.SetResponseMessage(
                    $"Sorry, that's not a valid format. Can you try again?\n\nRemember: hour:minute (hh:mm)", deleteMessage: true);

            await context.Bot.TryDeleteMessage(message.MessageId);
            context.Handled = true;
        }
    }
}