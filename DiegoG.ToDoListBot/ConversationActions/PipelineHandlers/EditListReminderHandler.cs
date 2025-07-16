using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Cronos;
using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Pipeline;
using GLV.Shared.ChatBot.Telegram;
using GLV.Shared.Data.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace DiegoG.ToDoListBot.ConversationActions.PipelineHandlers;

public class EditListReminderHandler : IChatBotPipelineHandler
{
    public async Task TryProcessKeyboardResponse(PipelineContext context, KeyboardResponse kr)
    {
        if (kr.TryGetIdFromData(ActionConstants.EditListReminder, out var listId)) // Edit task
        {
            await context.Bot.AnswerKeyboardResponse(kr, "Entered list reminder editing mode");
            await context.ActiveAction.SetResponseMessage("Please give me a cron string that expresses when you want to be reminded.\n\nIf you need help, please visit <a href=\"https://crontab.guru/\">this site!</a>\n\nOr if you want to remove the reminder, please simply write 'none'! (Without the quotes!)",
                deleteMessage: true,
                options: MessageOptions.HtmlContent);
            SetEditListId(context, listId);
            context.Context.SetStep(ActionConstants.EditListReminderStepSetName);
            context.Handled = true;
        }
    }

    public async Task TryProcessMessage(PipelineContext context, Message message)
    {
        if (string.IsNullOrWhiteSpace(message.Text) is false && context.Context.Step == ActionConstants.EditListReminderStepSetName)
        {
            if (GetEditListId(context, out var listid) is false)
                await context.ActiveAction.SetResponseMessage(
                    "Sorry, an error occurred on my end while trying to edit the list's reminder; can we try again?",
                    ToDoListKeyboards.ActionKeyboard, true);
            else
            {
                var chatid = context.Context.ConversationId.UnpackTelegramConversationId();
                string? newVal;
                if (message.Text.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    newVal = null;
                }
                else if (CronExpression.TryParse(message.Text, out var cronExpr) is true)
                {
                    newVal = message.Text;
                }
                else
                {
                    await context.ActiveAction.SetResponseMessage(
                        $"Sorry, the expression '{message.Text}' was not correct. Could you try again?\n\nBe sure to check <a href=\"https://crontab.guru/\">this site out if you need help!</a>.\n\nOr if you want to remove the reminder altogether, just write 'none'! (Without the quotes!)",
                        deleteMessage: true,
                        options: MessageOptions.HtmlContent
                    );

                    await context.Bot.TryDeleteMessage(message.MessageId);
                    return;
                }
                
                var modified = await context.Services.GetRequiredService<ToDoListDbContext>()
                    .ToDoLists
                    .Where(x => x.Id == listid && x.ChatId == chatid)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.CronExpression, newVal));

                if (modified == 1)
                {
                    await context.ActiveAction.SetResponseMessage(
                        "List reminder modified! What else can I do for you?",
                        ToDoListKeyboards.ActionKeyboard
                    );
                }
                else
                {
                    Debug.Assert(modified == 0);
                    await context.ActiveAction.SetResponseMessage(
                        "Couldn't modify this list's reminder, maybe it was deleted? What else can I do for you?",
                        ToDoListKeyboards.ActionKeyboard
                    );
                }
            }

            await context.Bot.TryDeleteMessage(message.MessageId);
            SetEditListId(context);
            context.Context.ResetState();
            context.MarkAsHandled();
        }
    }

    private const string ListIdDataKey = "reminder-list-id";

    private static void SetEditListId(PipelineContext context, long listid = default)
    {
        if (listid == default)
            context.Context.Data.Remove(ListIdDataKey);
        else
            context.Context.Data.Set(ListIdDataKey, listid);
    }

    private static bool GetEditListId(PipelineContext context, [MaybeNullWhen(false)] out long listid)
    {
        if (context.Context.Data.TryGetValue<long>(ListIdDataKey, out listid) is true)
            return true;

        listid = default;
        return false;
    }
}