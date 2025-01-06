using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using GLV.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationAction(nameof(RemoveListCommand), "removelist", "Adds a To-Do list to this chat")]
public class RemoveListCommand : ConversationActionBase
{
    protected override async Task<ConversationActionEndingKind> PerformAsync(UpdateContext update)
    {
        if (await CheckForCancellation())
            return ConversationActionEndingKind.Finished;

        if (Context.Step == 1)
        {
            if (update.Message is Message m and { Text: not null }) 
            {
                if (await RemoveList(m.Text))
                    await SuccessMessage();
                else
                    await NotFoundMessage();
                return ConversationActionEndingKind.Finished;
            }
            else if (update.KeyboardResponse is KeyboardResponse kr)
            {
                if (kr.Data?.TryGetTextAfter(Constants.ListDataHeader, out var text) is true && long.TryParse(text, out var listId))
                {
                    await RemoveList(listId);
                    await Bot.AnswerKeyboardResponse(kr, "The list has been deleted!");
                    await SuccessMessage();
                }
                else
                    await InvalidMessage();
            }
            else
                await SendKeyboard();
        }
        else
        {
            if (update.Message is not Message msg || string.IsNullOrWhiteSpace(msg.Text))
            {
                await InvalidMessage();
                return ConversationActionEndingKind.Finished;
            }
            else if (msg.Text.TryGetTextAfter(' ', out var text))
            {
                if (await RemoveList(text))
                    await SuccessMessage();
                else
                    await NotFoundMessage();
            }
            else
            {
                Context.SetState(1, nameof(RemoveListCommand));
                await SendKeyboard();
            }
        }

        async Task SendKeyboard()
        {
            var kb = await this.GetListKeyboard();
            if (kb is not Keyboard keyboard)
            {
                await Bot.RespondWithText("There are no lists to remove. What else can I do for you?");
                Context.ResetState();
                return;
            }

            await Bot.RespondWithKeyboard(keyboard, "Please choose a list to remove");
        }

        return ConversationActionEndingKind.Finished;

        Task SuccessMessage()
            => Bot.RespondWithText("The list has been deleted! What else can I do for you?");

        Task NotFoundMessage()
            => Bot.RespondWithText("Could not find the list to remove; please write /cancel if you want to do something else.");

        Task InvalidMessage()
            => Bot.RespondWithText("I'm sorry, I couldn't understand this message. Can you try again?");
    }

    private async Task<bool> RemoveList(string name)
    {
        var chatid = Context.ConversationId.UnpackTelegramConversationId();
        var changes = await Services.GetRequiredService<ToDoListDbContext>().ToDoLists
                                    .Where(x => x.Name == name && x.ChatId == chatid)
                                    .ExecuteDeleteAsync();
        if (changes == 0)
        {
            Context.SetStep(1);
            return false;
        }
        else
        {
            Debug.Assert(changes == 1);
            Context.ResetState();
            return true;
        }
    }

    private async Task<bool> RemoveList(long id)
    {
        var chatid = Context.ConversationId.UnpackTelegramConversationId();
        var changes = await Services.GetRequiredService<ToDoListDbContext>().ToDoLists
                                    .Where(x => x.Id == id && x.ChatId == chatid)
                                    .ExecuteDeleteAsync();
        if (changes == 0)
        {
            Context.SetStep(1);
            return false;
        }
        else
        {
            Debug.Assert(changes == 1);
            Context.ResetState();
            return true;
        }
    }
}
