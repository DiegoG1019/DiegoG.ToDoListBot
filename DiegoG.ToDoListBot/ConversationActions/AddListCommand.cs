using DiegoG.ToDoListBot.Data;
using DiegoG.ToDoListBot.Services;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using GLV.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types.Enums;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationAction(nameof(AddListCommand), "addlist", "Adds a To-Do list to this chat")]
public class AddListCommand : ConversationActionBase
{
    protected override async Task<ConversationActionEndingKind> PerformAsync(UpdateContext update)
    {
        if (await CheckForCancellation())
            return ConversationActionEndingKind.Finished;

        if (update.Message is not Message msg || string.IsNullOrWhiteSpace(msg.Text)) 
            return ConversationActionEndingKind.Finished;

        if (Context.Step == 1)
            await AddList(msg.Text);
        else
        {
            if (msg.Text.TryGetTextAfter(' ', out var listName) is false)
            {
                Context.SetState(1, nameof(AddListCommand));
                await Bot.RespondWithText("Please tell me the name of the list, or write /cancel if you want to do something else.");
            }
            else
                await AddList(listName);
        }

        async Task AddList(string name)
        {
            var chatId = update.ConversationId.UnpackTelegramConversationId();
            var db = Services.GetRequiredService<ToDoListDbContext>();
            if (string.IsNullOrWhiteSpace(name) || await db.ToDoLists.AnyAsync(x => x.Name == name && x.ChatId == chatId))
                await Bot.RespondWithText("I can't add a list with that name; maybe there's already a list with that name? please give me another one, or write /cancel if you want to do something else.");
            else
            {
                db.ToDoLists.AddNewToDoList(chatId, name);
                await db.SaveChangesAsync();

                Context.ResetState();
                await Bot.RespondWithText("The list has been added! What else can I do for you?");
            }
        }

        return ConversationActionEndingKind.Finished;
    }
}
