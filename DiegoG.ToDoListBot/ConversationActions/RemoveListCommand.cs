using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationAction(nameof(RemoveListCommand), "removelist", "Adds a To-Do list to this chat")]
public class RemoveListCommand : ConversationActionBase
{
    public class ListInfo(string? name, long id, string callbackId)
    {
        public string? Name { get; init; } = name;
        public long Id { get; init; } = id;
        public string CallbackId { get; set; } = callbackId;
    }

    protected override async Task<ConversationActionEndingKind> PerformAsync(UpdateContext update)
    {
        // Display an inline keyboard

        if (update is not TelegramUpdateContext tl
            || tl.Update.Type is not UpdateType.Message
            || string.IsNullOrWhiteSpace(tl.Update.Message!.Text))
            return ConversationActionEndingKind.Finished;

        if (Context.Step == 1)
            await RemoveList(tl.Update.Message!.Text);
        else
        {
            var text = tl.Update.Message!.Text;
            var firstSpace = text.IndexOf(' ');
            if (firstSpace == -1 || firstSpace + 1 >= text.Length)
            {
                Context.SetState(1, nameof(RemoveListCommand));
                if (Bot.UnderlyingBotClientObject is WTelegram.Bot bot)
                {
                    var keys = new List<InlineKeyboardButton>();
                    await foreach (var list in Services.GetRequiredService<ToDoListDbContext>()
                                                       .ToDoLists.Select(x => new ListInfo(x.Name!, x.Id, null!))
                                                       .AsAsyncEnumerable())
                    {
                        var btn = new InlineKeyboardButton()
                        {
                            Text = list.Name ?? $"List #{list.Id}",
                            CallbackData = $"ListId:{list.Id}"
                        };

                        keys.Add(btn);
                    }

                    await Bot.RespondWithKeyboard()
                }
                else
                    await Bot.RespondWithText("Non-telegram platforms not supported");
            }
            else
                await RemoveList(text[(firstSpace + 1)..]);
        }

        async Task RemoveList(string name)
        {
            var chatid = update.ConversationId.UnpackTelegramConversationId();
            var changes = await Services.GetRequiredService<ToDoListDbContext>().ToDoLists
                                        .Where(x => x.Name == name && x.ChatId == chatid)
                                        .ExecuteDeleteAsync();
            if (changes == 0)
            {
                Debug.Assert(changes == 1);
                Context.SetStep(1);
                await Bot.RespondWithText("Could not find the list");
            }
            else
            {
                Context.ResetState();
                await Bot.RespondWithText("The list has been deleted!");
            }
        }

        return ConversationActionEndingKind.Finished;
    }
}
