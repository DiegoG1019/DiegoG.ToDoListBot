using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Pipeline;
using GLV.Shared.Data.Identifiers;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using TL;
using WTelegram;

namespace DiegoG.ToDoListBot.ConversationActions;

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
                    "Sorry, an error ocurred on my end. Can we try again?", 
                    ToDoListKeyboards.ActionKeyboard, 
                    true
                );
            }

            await AddTaskItems(context, message.Text.ReplaceLineEndings("\0\0!\0\0").Split("\0\0!\0\0"));
            await context.ActiveAction.SetResponseMessage("Tasks added!", await context.ActiveAction.GetListItems(listId));
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

        else if (kr.TryGetIdFromData(ActionConstants.ViewListHeader, out var listid)) // Get List
        {
            var kb = await context.ActiveAction.GetListItems(listid);
            if (kb is null)
                await context.Bot.AnswerKeyboardResponse(kr, "Could not find the list to view it");
            else
                await context.ActiveAction.SetResponseMessage("Here are your tasks!", kb);

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.AddTaskHeader, out listid)) // Add Task to List
        {
            await context.ActiveAction.SetResponseMessage("Please tell me the name of the task. You may add more than one by putting each in a different line!");
            context.Context.SetStep(ActionConstants.AddTaskStepName);
            SetListId(context.Context, listid);

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.ViewTaskHeader, out var taskid)) // View Task details
        {
            /*
             * 
            row.Add(new KeyboardKey(
                "🗑️",
                $"{Constants.TaskDeleteHeader}{tid}"
            ));

            row.Add(new KeyboardKey(
                "📝",
                $"{Constants.TaskEditHeader}{tid}"
            ));
             * 
             */
            await context.Bot.AnswerKeyboardResponse(kr, "I'm sorry, task descriptions are not supported yet 😭");

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.EditTaskHeader, out taskid)) // Edit task
        {
            await context.Bot.AnswerKeyboardResponse(kr, "I'm sorry, editing tasks is not supported yet 😔");

            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.DeleteTaskHeader, out taskid)) // Delete Task
        {
            await context.Bot.AnswerKeyboardResponse(kr, "I'm sorry, deleting tasks is not supported yet 😒");

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
        if (context.Data.TryGetValue(ListIdDataKey, out var listId_str))
        {
            listId = long.Parse(listId_str);
            return true;
        }

        listId = default;
        return false;
    }

    private static void SetListId(ConversationContext context, long listId = default)
    {
        if (listId == default)
            context.Data.Remove(ListIdDataKey);
        else
            context.Data[ListIdDataKey] = listId.ToString();
    }

    private static async Task AddTaskItems(PipelineContext pipeline, params string[] tasks)
    {
        if (pipeline.Context.Data.TryGetValue(ListIdDataKey, out var id_str) is false)
            Debug.Fail("Tried to add a task to a list with no set list id in Data");

        var listId = long.Parse(id_str);
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
