using DiegoG.ToDoListBot.Data;
using DiegoG.ToDoListBot.Services;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DiegoG.ToDoListBot.ConversationActions;

public static class ToDoListConversationHelper
{
    public static bool TryGetIdFromData(this KeyboardResponse kr, string header, out long id)
    {
        Debug.Assert(kr.Data is not null);
        if (kr.Data.StartsWith(header))
        {
            id = long.Parse(kr.Data.AsSpan()[header.Length..]);
            return true;
        }

        id = default;
        return false;
    }

    private static Keyboard? WrapUpTaskKeyboard(List<KeyboardRow> keys, long listId)
    {
        keys.Add(new KeyboardRow(new KeyboardKey("Add Task", $"{Constants.TaskAddHeader}{listId}")));
        return new Keyboard(keys);
    }

    private static void AppendTaskRow(List<KeyboardRow> rows, TaskInfo task)
    {
        List<KeyboardKey> row = [];

        long tid = task.Id.AsLong();

        row.Add(new KeyboardKey(
            task.Name ?? $"Task #{tid}",
            $"{Constants.TaskViewHeader}{tid}"
        ));

        row.Add(new KeyboardKey(
            task.IsCompleted ? "✔️" : "⭕",
            $"{Constants.TaskToggleHeader}{tid}"
        ));

        rows.Add(new KeyboardRow(row));
    }

    public static async Task<Keyboard?> GetListItemsFromTask(this ConversationActionBase action, long taskId, ToDoListDbContext? context = null)
    {
        var keys = new List<KeyboardRow>();
        var snowflake = new GLV.Shared.Data.Identifiers.Snowflake(taskId);
        var id = action.Context.ConversationId.UnpackTelegramConversationId();
        var q = (context ?? action.Services.GetRequiredService<ToDoListDbContext>()).ToDoTasks
            .Where(x => x.ToDoList!.ChatId == id && x.ToDoList!.Tasks!.Any(x => x.Id == snowflake));

        long listId;
        if ((listId = await q.Select(x => x.ToDoListId).FirstOrDefaultAsync()) == default) 
            return null;

        await foreach (var task in q.Select(x => new TaskInfo(x.Name!, x.Id, x.IsCompleted, null)).AsAsyncEnumerable())
            AppendTaskRow(keys, task);

        return WrapUpTaskKeyboard(keys, listId);
    }

    public static async Task<Keyboard?> GetListItems(this ConversationActionBase action, long listId, ToDoListDbContext? context = null)
    {
        var keys = new List<KeyboardRow>();
        var id = action.Context.ConversationId.UnpackTelegramConversationId();
        var q = (context ?? action.Services.GetRequiredService<ToDoListDbContext>()).ToDoLists
            .Where(x => x.ChatId == id && x.Id == listId);

        if (await q.AnyAsync() is false)
            return null;

        q = q.Where(x => x.Tasks != null);

        await foreach (var task in q.SelectMany(x => x.Tasks!).Select(x => new TaskInfo(x.Name!, x.Id, x.IsCompleted, null)).AsAsyncEnumerable())
            AppendTaskRow(keys, task);

        return WrapUpTaskKeyboard(keys, listId);
    }

    public static async Task<Keyboard?> GetListKeyboard(this ConversationActionBase action)
    {
        var keys = new List<KeyboardRow>();
        var id = action.Context.ConversationId.UnpackTelegramConversationId();
        var q = action.Services.GetRequiredService<ToDoListDbContext>().ToDoLists
            .Where(x => x.ChatId == id)
            .Select(x => new ListInfo(x.Name!, x.Id));

        if (await q.AnyAsync() is false)
            return null;

        await foreach (var list in q.AsAsyncEnumerable())
        {
            var btn = new KeyboardKey(
                list.Name ?? $"List #{list.Id}",
                $"{Constants.ListDataHeader}{list.Id}"
            );

            keys.Add(new KeyboardRow(btn));
        }

        return new Keyboard(keys);
    }
}
