using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Pipeline;
using GLV.Shared.ChatBot.Telegram;
using GLV.Shared.Data.Identifiers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ToDoListBot.ConversationActions.PipelineHandlers;
public class TaskHandler : IChatBotPipelineHandler
{
    public async Task TryProcessKeyboardResponse(PipelineContext context, KeyboardResponse kr)
    {
        if (kr.TryGetIdFromData(ActionConstants.EditTaskHeader, out var taskid)) // Edit task
        {
            await context.Bot.AnswerKeyboardResponse(kr, "Entered task editing mode");
            await context.ActiveAction.SetResponseMessage("Please tell me the new name of the task", deleteMessage: true);
            SetEditTaskId(context, taskid);
            context.Context.SetStep(ActionConstants.EditTaskStepSetName);
            context.Handled = true;
        }

        else if (kr.TryGetIdFromData(ActionConstants.DeleteTaskHeader, out taskid)) // Delete Task
        {
            var chatid = context.Context.ConversationId.UnpackTelegramConversationId();
            var snowflake = new Snowflake(taskid);
            var changes = await context.Services.GetRequiredService<ToDoListDbContext>().ToDoTasks
                                        .Where(x => x.Id == snowflake && x.ToDoList!.ChatId == chatid)
                                        .ExecuteDeleteAsync();

            if (changes == 0)
                await context.Bot.AnswerKeyboardResponse(kr, "The task was not deleted, maybe it was already gone?");
            else
            {
                Debug.Assert(changes == 1);
                context.Context.ResetState();
                await context.Bot.AnswerKeyboardResponse(kr, "Task deleted!");
                var keyb = await context.ActiveAction.GetListItemsFromTask(taskid);
                if (keyb is not Keyboard kb)
                    await context.ActiveAction.SetResponseMessage("What else can I do for you?", ToDoListKeyboards.ActionKeyboard);
                else
                    await context.ActiveAction.SetResponseMessage("What else can I do for you?", kb);
            }

            context.Handled = true;
        }
    }

    public async Task TryProcessMessage(PipelineContext context, Message message)
    {
        if (string.IsNullOrWhiteSpace(message.Text) is false && context.Context.Step == ActionConstants.EditTaskStepSetName)
        {
            if (GetEditTaskId(context, out var taskid)) 
            {
                var snowflake = new Snowflake(taskid);
                var chatid = context.Context.ConversationId.UnpackTelegramConversationId();
                var name = ToDoListTask.SanitizeName(message.Text);
                var modified = await context.Services.GetRequiredService<ToDoListDbContext>()
                                                     .ToDoTasks
                                                     .Where(x => x.Id == snowflake && x.ToDoList!.ChatId == chatid)
                                                     .ExecuteUpdateAsync(x => x.SetProperty(p => p.Name, name));

                if (modified == 1)
                {
                    await context.ActiveAction.SetResponseMessage(
                        "Task modified! What else can I do for you?",
                        ToDoListKeyboards.ActionKeyboard
                    );
                }
                else
                {
                    Debug.Assert(modified == 0);
                    await context.ActiveAction.SetResponseMessage(
                        "Couldn't modify the task, maybe it was deleted? What else can I do for you?",
                        ToDoListKeyboards.ActionKeyboard
                    );
                }

                await context.Bot.TryDeleteMessage(message.MessageId);
            }
            else
                await context.ActiveAction.SetResponseMessage("Sorry, an error ocurred on my end while trying to edit the task; can we try again?", ToDoListKeyboards.ActionKeyboard, true);

            SetEditTaskId(context);
            context.Context.ResetState();
            context.MarkAsHandled();
        }
    }

    private const string TaskIdDataKey = "edit-task-id";

    private static void SetEditTaskId(PipelineContext context, long taskid = default)
    {
        if (taskid == default)
            context.Context.Data.Remove(TaskIdDataKey);
        else
            context.Context.Data.Set(TaskIdDataKey, taskid);
    }

    private static bool GetEditTaskId(PipelineContext context, [MaybeNullWhen(false)] out long taskid)
    {
        if (context.Context.Data.TryGetValue<long>(TaskIdDataKey, out taskid) is true)
            return true;

        taskid = default;
        return false;
    }
}
