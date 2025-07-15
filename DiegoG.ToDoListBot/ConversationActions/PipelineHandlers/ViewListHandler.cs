using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Pipeline;
using GLV.Shared.ChatBot.Telegram;
using GLV.Shared.Data.Identifiers;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using Humanizer;
using TL;
using WTelegram;

namespace DiegoG.ToDoListBot.ConversationActions.PipelineHandlers;

public class ViewListHandler : IChatBotPipelineKeyboardHandler, IChatBotPipelineMessageHandler
{
    private const string ListIdDataKey = "view-list-id";

    public async Task TryProcessMessage(PipelineContext context, GLV.Shared.ChatBot.Message message)
    {
        if (string.IsNullOrWhiteSpace(message.Text) is false && context.Context.Step == ActionConstants.AddTaskStepName)
        {
            if (TryGetListId(context.Context, out var listId) is false)
            {
                await context.ActiveAction.SetResponseMessage(
                    "Sorry, an error occurred on my end. Can we try again?",
                    ToDoListKeyboards.ActionKeyboard,
                    true
                );
            }

            var (keyb, cron) = await context.ActiveAction.GetListItems(listId);
            var msg = cron.TryGetCronNextReminder(out var rm)
                ? $"Tasks added!\n\nThis list's next reminder will ring in <i>{rm.Humanize()}</i>"
                : "Tasks added!";

            await AddTaskItems(context, message.Text.ReplaceLineEndings("\0\0!\0\0").Split("\0\0!\0\0"));
            await context.ActiveAction.SetResponseMessage(msg, keyb);
            await context.Bot.TryDeleteMessage(message.MessageId);
            context.Context.ResetState();
            context.Handled = true;
        }
    }

    public async Task TryProcessKeyboardResponse(PipelineContext context, KeyboardResponse kr)
    {
        if (kr.MatchDataTag(ActionConstants.ViewMainMenuTag)) // Return to main menu
        {
            await context.ActiveAction.SetResponseMessage("Here are your lists!", ToDoListKeyboards.ActionKeyboard);
            context.Handled = true;
        }

        else if (kr.MatchDataTag(ActionConstants.ViewListsTag)) // View lists
        {
            var kb = await context.ActiveAction.GetListKeyboard();
            if (kb is null)
                await context.Bot.AnswerKeyboardResponse(kr, "There are no available lists in this chat, sorry!");
            else
                await context.ActiveAction.SetResponseMessage("Here are your tasks!", kb);

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.ViewListFromTaskHeader, out var taskid)) // View list from a task id
        {
            var kb = await context.ActiveAction.GetListItemsFromTask(taskid);
            if (kb is null)
                await context.Bot.AnswerKeyboardResponse(kr, "Could not find the list to view it");
            else
                await context.ActiveAction.SetResponseMessage("Here are your tasks!", kb);

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.ViewListHeader, out var listid)) // Get List
        {
            var (keyb, cron) = await context.ActiveAction.GetListItems(listid);

            if (keyb is null)
                await context.Bot.AnswerKeyboardResponse(kr, "Could not find the list to view it");
            else
            {
                var msg = cron.TryGetCronNextReminder(out var rm)
                    ? $"Here are your tasks!\n\n<i>This list's next reminder will ring in {rm.Humanize()}</i>"
                    : "Here are your tasks!";
                await context.ActiveAction.SetResponseMessage(msg, keyb, options: MessageOptions.HtmlContent);
            }

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.AddTaskHeader, out listid)) // Add Task to List
        {
            await context.ActiveAction.SetResponseMessage("Please tell me the name of the task. You may add more than one by putting each in a different line!");
            context.Context.SetStep(ActionConstants.AddTaskStepName);
            SetListId(context.Context, listid);

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.ViewTaskHeader, out taskid)) // View Task details
        {
            var snowflake = new Snowflake(taskid);
            var chatId = context.Update.ConversationId.UnpackTelegramConversationId();
            var task = await context.Services.GetRequiredService<ToDoListDbContext>()
                                    .ToDoTasks.FirstOrDefaultAsync(x => x.Id == snowflake && x.ToDoList!.ChatId == chatId);

            if (task is null)
            {
                await context.Bot.AnswerKeyboardResponse(kr, "Couldn't find the task 😔");

                var keyb = await context.ActiveAction.GetListItemsFromTask(taskid);
                if (keyb is not Keyboard kb)
                    await context.ActiveAction.SetResponseMessage(
                        $"An error ocurred on my end; can we try something else?",
                        ToDoListKeyboards.ActionKeyboard
                    );
                else
                    await context.ActiveAction.SetResponseMessage(
                        $"Sorry, I couldn't find the task; can we try something else?",
                        kb
                    );
            }
            else
            {
                await context.Bot.AnswerKeyboardResponse(kr, "Found it!");
                await context.ActiveAction.SetResponseMessage(
                    $"This is the information relating to the task at hand:\n <u>{task.Name}</u> \n\nStatus: {(task.IsCompleted ? "Completed" : "Not yet completed")}", 
                    ToDoListKeyboards.GetTaskInfoKeyboard(taskid),
                    options: MessageOptions.HtmlContent
                );
            }

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.ExportListHeader, out listid))
        {
            await context.ActiveAction.SetResponseMessage("Please select an export format", ToDoListKeyboards.GetListExportKeyboard(listid));
            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.ToggleTaskHeader, out taskid)) // Toggle the task status
        {
            var snowflake = new Snowflake(taskid);
            var db = context.Services.GetRequiredService<ToDoListDbContext>();
            var modified = await db.ToDoTasks.Where(x => x.Id == snowflake)
                                             .ExecuteUpdateAsync(set => set.SetProperty(prop => prop.IsCompleted, prop => !prop.IsCompleted));

            if (modified == 0)
                await context.Bot.AnswerKeyboardResponse(kr, "Could not find the task, sorry 😔");

            var kb = await context.ActiveAction.GetListItemsFromTask(taskid, db);

            if (kb is Keyboard keyboard)
            {
                await context.ActiveAction.SetResponseMessage("Here are your tasks!", kb);
                await context.Bot.AnswerKeyboardResponse(kr, "Task toggled!");
            }
            else
            {
                await context.ActiveAction.SetResponseMessage("An error ocurred while trying to re-display the list. Please try again.", ToDoListKeyboards.ActionKeyboard);

                await context.Bot.AnswerKeyboardResponse(kr, "Task toggled!");
            }

            context.Handled = true;
        }
    }

    private static bool TryGetListId(ConversationContext context, out long listId)
    {
        if (context.Data.TryGetValue<long>(ListIdDataKey, out listId) is true)
            return true;

        listId = default;
        return false;
    }

    private static void SetListId(ConversationContext context, long listId = default)
    {
        if (listId == default)
            context.Data.Remove(ListIdDataKey);
        else
            context.Data.Set(ListIdDataKey, listId);
    }

    private static async Task AddTaskItems(PipelineContext pipeline, params string[] tasks)
    {
        if (pipeline.Context.Data.TryGetValue<long>(ListIdDataKey, out var listId) is false)
            Debug.Fail("Tried to add a task to a list with no set list id in Data");

        var db = pipeline.Services.GetRequiredService<ToDoListDbContext>();

        for (int i = 0; i < tasks.Length; i++)
            db.ToDoTasks.Add(new ToDoListTask()
            {
                Id = Snowflake.New(),
                Name = tasks[i],
                ToDoListId = listId,
            });

        await db.SaveChangesAsync();
        pipeline.Context.Data.Remove(ListIdDataKey);
    }
}
