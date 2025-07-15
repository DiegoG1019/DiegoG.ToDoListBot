using DiegoG.ToDoListBot.Data;
using DiegoG.ToDoListBot.Services;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;

namespace DiegoG.ToDoListBot;

public static class ToDoListKeyboards
{
    public static Keyboard GetTaskInfoKeyboard(long taskId)
        => new Keyboard(
            new KeyboardRow(new KeyboardKey("Delete Task", $"{ActionConstants.DeleteTaskHeader}{taskId}"), new KeyboardKey("Edit Task", $"{ActionConstants.EditTaskHeader}{taskId}")),
            new KeyboardRow(new KeyboardKey("Back to List", $"{ActionConstants.ViewListFromTaskHeader}{taskId}"))
        );

    public static Keyboard GetListExportKeyboard(long listId)
        => new Keyboard(
            new KeyboardRow(new KeyboardKey("Text", $"{ActionConstants.ExportListTextHeader}{listId}"))
        );

    private static Keyboard? WrapUpTaskKeyboard(List<KeyboardRow> keys, long listId)
    {
        keys.Add(new KeyboardRow(
            new KeyboardKey("Add Task", $"{ActionConstants.AddTaskHeader}{listId}"),
            new KeyboardKey("Back to List", ActionConstants.ViewListsTag),
            new KeyboardKey("Export List", $"{ActionConstants.ExportListHeader}{listId}"),
            new KeyboardKey("Set reminder", $"{ActionConstants.EditListReminder}{listId}")
        ));
        return new Keyboard(keys);
    }

    private static void AppendTaskRow(List<KeyboardRow> rows, TaskInfo task)
    {
        List<KeyboardKey> row = [];

        long tid = task.Id.AsLong();

        row.Add(new KeyboardKey(
            task.Name ?? $"Task #{tid}",
            $"{ActionConstants.ViewTaskHeader}{tid}"
        ));

        row.Add(new KeyboardKey(
            task.IsCompleted ? "✅" : "⭕",
            $"{ActionConstants.ToggleTaskHeader}{tid}"
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

    public static async Task<(Keyboard? keyboard, string? cron)> GetListItems(this ConversationActionBase action, long listId, ToDoListDbContext? context = null)
    {
        var keys = new List<KeyboardRow>();
        var id = action.Context.ConversationId.UnpackTelegramConversationId();
        var q = (context ?? action.Services.GetRequiredService<ToDoListDbContext>()).ToDoLists
            .Where(x => x.ChatId == id && x.Id == listId);

        if (await q.AnyAsync() is false)
            return default;

        q = q.Where(x => x.Tasks != null);

        var list = await q.Select(list => new ListItemsInfo(
                list.CronExpression,
                list.Tasks!.Select(task => new TaskInfo(task.Name!, task.Id, task.IsCompleted, null))))
            .SingleOrDefaultAsync();

        return list is null ? default : (WrapUpTaskKeyboard(keys, listId), list.Cron);
    }

    public static Keyboard ActionKeyboard { get; } = new(
        new KeyboardRow(
            new KeyboardKey("View Lists", ActionConstants.ViewListsTag)
        ),
        new KeyboardRow(
            new KeyboardKey("Remove List", ActionConstants.RemoveListTag),
            new KeyboardKey("Add List", ActionConstants.AddListTag)
        )
    );

    public static async Task<Keyboard?> GetListKeyboard(this ConversationActionBase action, string? header = null)
    {
        header ??= ActionConstants.ViewListHeader;
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
                $"{header}{list.Id}"
            );

            keys.Add(new KeyboardRow(btn));
        }

        keys.Add(new KeyboardRow(new KeyboardKey("Return to Main Menu", ActionConstants.ViewMainMenuTag)));
        return new Keyboard(keys);
    }
}
