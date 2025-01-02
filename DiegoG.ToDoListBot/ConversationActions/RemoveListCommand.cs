using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using GLV.Shared.Common;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationAction(nameof(RemoveListCommand), "removelist", "Adds a To-Do list to this chat")]
public class RemoveListCommand : ConversationActionBase
{
    public class ListInfo(string? name, long id)
    {
        public string? Name { get; init; } = name;
        public long Id { get; init; } = id;
    }

    protected override async Task<ConversationActionEndingKind> PerformAsync(UpdateContext update)
    {
        if (Context.Step == 1)
        {
            if (update.Message is Message m and { Text: not null }) 
            {
                await RemoveList(m.Text);
                return ConversationActionEndingKind.Finished;
            }
            else if (update.KeyboardResponse is KeyboardResponse kr)
            {
                if (kr.Data?.TryGetTextAfter(Constants.ListDataHeader, out var text) is true && long.TryParse(text, out var listId))
                    await RemoveList(listId);
                else
                    await Bot.RespondWithText("I'm sorry, I couldn't understand this message. Can you try again?");
            }
            else
                await SendKeyboard();
        }
        else
        {
            if (update.Message is not Message msg || string.IsNullOrWhiteSpace(msg.Text))
            {
                await Bot.RespondWithText("I'm sorry, I couldn't understand this message. Can you try again?");
                return ConversationActionEndingKind.Finished;
            }
            else if (msg.Text.TryGetTextAfter(' ', out var text))
                await RemoveList(text);
            else
            {
                Context.SetState(1, nameof(RemoveListCommand));
                await SendKeyboard();
            }
        }

        async Task SendKeyboard()
        {
            var keys = new List<KeyboardRow>();
            await foreach (var list in Services.GetRequiredService<ToDoListDbContext>()
                                               .ToDoLists.Select(x => new ListInfo(x.Name!, x.Id))
                                               .AsAsyncEnumerable())
            {
                var btn = new KeyboardKey(
                    list.Name ?? $"List #{list.Id}",
                    $"{Constants.ListDataHeader}{list.Id}"
                );

#error Check if there are any lists available
#error Add a command to list lists (and view them)

                keys.Add(new KeyboardRow(btn));
            }

            await Bot.RespondWithKeyboard(new Keyboard(keys), "Please choose a list to remove");
        }

        return ConversationActionEndingKind.Finished;
    }

    private async Task RemoveList(string name)
    {
        var chatid = Context.ConversationId.UnpackTelegramConversationId();
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

    private async Task RemoveList(long id)
    {
        var chatid = Context.ConversationId.UnpackTelegramConversationId();
        var changes = await Services.GetRequiredService<ToDoListDbContext>().ToDoLists
                                    .Where(x => x.Id == id && x.ChatId == chatid)
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
}
