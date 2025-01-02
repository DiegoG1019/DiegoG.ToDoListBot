using DiegoG.ToDoListBot.Data;
using DiegoG.ToDoListBot.Services;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using Telegram.Bot.Types.Enums;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationAction(nameof(AddListCommand), "addlist", "Adds a To-Do list to this chat")]
public class AddListCommand : ConversationActionBase
{
    protected override async Task<ConversationActionEndingKind> PerformAsync(UpdateContext update)
    {
        if (update is not TelegramUpdateContext tl
            || tl.Update.Type is not UpdateType.Message
            || string.IsNullOrWhiteSpace(tl.Update.Message!.Text)) 
            return ConversationActionEndingKind.Finished;

        if (Context.Step == 1)
            await AddList(tl.Update.Message!.Text);
        else
        {
            var text = tl.Update.Message!.Text;
            var firstSpace = text.IndexOf(' ');
            if (firstSpace == -1 || firstSpace + 1 >= text.Length)
            {
                Context.SetState(1, nameof(AddListCommand));
                await Bot.RespondWithText("Please tell me the name of the list");
            }
            else
                await AddList(text[(firstSpace + 1)..]);
        }

        async Task AddList(string name)
        {
            Services.GetRequiredService<ToDoListDbContext>().ToDoLists
                    .AddNewToDoList(update.ConversationId.UnpackTelegramConversationId(), name);

            Context.ResetState();
            await Bot.RespondWithText("The list has been added!");
        }

        return ConversationActionEndingKind.Finished;
    }
}
