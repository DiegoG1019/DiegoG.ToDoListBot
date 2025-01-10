using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Pipeline;
using GLV.Shared.ChatBot.Telegram;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DiegoG.ToDoListBot.ConversationActions;

public class RemoveListHandler : IChatBotPipelineKeyboardHandler
{
    public async Task TryProcessKeyboardResponse(PipelineContext context, KeyboardResponse kr)
    {
        if (kr.MatchDataTag(ActionConstants.RemoveListTag))
        {
            var kb = await context.ActiveAction.GetListKeyboard(ActionConstants.DeleteListDataHeader);
            if (kb is not Keyboard keyboard)
            {
                await context.ActiveAction.SetResponseMessage(
                    "There are no lists to remove. What else can I do for you?",
                    ToDoListKeyboards.ActionKeyboard
                );
            }
            else
                await context.ActiveAction.SetResponseMessage("Please choose a list to remove", keyboard);

            context.Handled = true;
        }
        else if (kr.TryGetIdFromData(ActionConstants.DeleteListDataHeader, out long listId))
        {
            var chatid = context.Context.ConversationId.UnpackTelegramConversationId();
            var changes = await context.Services.GetRequiredService<ToDoListDbContext>().ToDoLists
                                        .Where(x => x.Id == listId && x.ChatId == chatid)
                                        .ExecuteDeleteAsync();

            if (changes == 0)
                await context.Bot.AnswerKeyboardResponse(kr, "The list was not deleted, maybe it was already gone?");
            else
            {
                Debug.Assert(changes == 1);
                context.Context.ResetState();
                await context.Bot.AnswerKeyboardResponse(kr, "List deleted!");
                await context.ActiveAction.SetResponseMessage("What else can I do for you?", ToDoListKeyboards.ActionKeyboard);
            }

            context.Handled = true;
        }
    }
}
