using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.Data.Identifiers;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using TL;
using Message = GLV.Shared.ChatBot.Message;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationAction(nameof(ViewListCommand), "lists", "View the lists in this chat")]
public class ViewListCommand : ConversationActionBase
{
    private const string KeyboardIdDataKey = "view-kb-msg-id";
    private const string ListIdDataKey = "view-list-id";

    protected override async Task<ConversationActionEndingKind> PerformAsync(UpdateContext update)
    {
        if (await CheckForCancellation())
            return ConversationActionEndingKind.Finished;

        if (update.Message is Message msg)
        {
            if (Context.Step is 0) // Step is 0, means the default state
            {
                var kb = await this.GetListKeyboard();
                if (kb is Keyboard keyboard)
                {
                    SetStoredKeyboard(await Bot.RespondWithKeyboard(keyboard, "Please select the list you want to view"));
                    Context.Data.Remove(KeyboardIdDataKey);
                    return ConversationActionEndingKind.Finished;
                }

                await Bot.RespondWithText("There are no available lists in this chat. What else can I do for you?");
                Context.ResetState();
                return ConversationActionEndingKind.Finished;
            }
            else if (Context.Step is 1) // Step is 1, means we're adding a Task
            {
                if (string.IsNullOrWhiteSpace(msg.Text))
                {
                    await InvalidMessage();
                    return ConversationActionEndingKind.Finished;
                }

                await AddTaskItems(msg.Text.ReplaceLineEndings("\0\0").Split("\0\0"));
                await Bot.RespondWithText("Tasks added! What else can I do for you?");
                Context.SetStep(0);
                return ConversationActionEndingKind.Finished;
            }
        }
        else if (update.KeyboardResponse is KeyboardResponse kr && Context.Step is 0) 
        {
            if (string.IsNullOrWhiteSpace(kr.Data))
            {
                await InvalidMessage(kr);
                return ConversationActionEndingKind.Finished;
            }

            else if (kr.TryGetIdFromData(Constants.ListDataHeader, out var listid)) // Get List
            {
                var kb = await this.GetListItems(listid);
                if (kb is not Keyboard keyboard)
                {
                    await ListNotFoundMessage(kr);
                    return ConversationActionEndingKind.Finished;
                }

                await Bot.AnswerKeyboardResponse(kr, "Found it!");
                if (GetStoredKeyboard(out var kbid))
                    await Bot.EditKeyboard(kbid, keyboard, null);
                else
                    SetStoredKeyboard(await Bot.RespondWithKeyboard(keyboard, "Here are your tasks!"));

                return ConversationActionEndingKind.Finished;
            }

            else if (kr.TryGetIdFromData(Constants.TaskAddHeader, out listid)) // Add Task to List
            {
                if (GetStoredKeyboard(out var kbid))
                    await Bot.DeleteMessage(kbid);

                await Bot.RespondWithText("Please tell me the name of the task. You may add more than one by putting each in a different line!");
                Context.SetStep(1);
                SetListId(listid);
                return ConversationActionEndingKind.Finished;
            }

            else if (kr.TryGetIdFromData(Constants.TaskViewHeader, out var taskid)) // View Task details
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
                await Bot.RespondWithText("I'm sorry, task descriptions are not supported yet 😭");
                return ConversationActionEndingKind.Finished;
            }

            else if (kr.TryGetIdFromData(Constants.TaskEditHeader, out taskid)) // Edit task
            {
                await Bot.RespondWithText("I'm sorry, editing tasks is not supported yet 😔");
                return ConversationActionEndingKind.Finished;
            }

            else if (kr.TryGetIdFromData(Constants.TaskDeleteHeader, out taskid)) // Delete Task
            {
                var snowflake = new Snowflake(taskid);
                var db = Services.GetRequiredService<ToDoListDbContext>();
                var modified = await db.ToDoTasks.Where(x => x.Id == snowflake).ExecuteDeleteAsync();

                if (modified == 0)
                {
                    await TaskNotFoundMessage(kr);
                    return ConversationActionEndingKind.Finished;
                }

                var kb = await this.GetListItemsFromTask(taskid, db);
                if (kb is not Keyboard keyboard)
                {
                    await ListNotFoundMessage(kr);
                    return ConversationActionEndingKind.Finished;
                }

                if (GetStoredKeyboard(out var kbid))
                    try
                    {
                        await Bot.EditKeyboard(kbid, keyboard, null);
                    }
                    catch (RpcException)
                    {
                        SetStoredKeyboard(await Bot.RespondWithKeyboard(keyboard, "Here are your tasks!"));
                    }

                return ConversationActionEndingKind.Finished;
            }

            else if (kr.TryGetIdFromData(Constants.TaskToggleHeader, out taskid)) // Toggle the task status
            {
                var snowflake = new Snowflake(taskid);
                var db = Services.GetRequiredService<ToDoListDbContext>();
                var modified = await db.ToDoTasks.Where(x => x.Id == snowflake)
                                                 .ExecuteUpdateAsync(set => set.SetProperty(prop => prop.IsCompleted, prop => !prop.IsCompleted));

                if (modified == 0)
                {
                    await TaskNotFoundMessage(kr);
                    return ConversationActionEndingKind.Finished;
                }

                var kb = await this.GetListItemsFromTask(taskid, db);
                if (kb is not Keyboard keyboard)
                {
                    await ListNotFoundMessage(kr);
                    return ConversationActionEndingKind.Finished;
                }

                if (GetStoredKeyboard(out var kbid))
                    try
                    {
                        await Bot.EditKeyboard(kbid, keyboard, null);
                    }
                    catch (RpcException)
                    {
                        SetStoredKeyboard(await Bot.RespondWithKeyboard(keyboard, "Here are your tasks!"));
                    }

                return ConversationActionEndingKind.Finished;
            }

            else
            {
                await InvalidMessage(kr);
                return ConversationActionEndingKind.Finished;
            }
        }

        throw new InvalidProgramException("This point should not be reached -- All branches should return on their own");
    }

    private bool GetStoredKeyboard([MaybeNullWhen(false)] out long id)
    {
        if (Context.Data.TryGetValue(KeyboardIdDataKey, out var kid))
        {
            id = long.Parse(kid);
            return true;
        }

        id = default;
        return false;
    }
    
    private void SetStoredKeyboard(long id = default)
    {
        if (id == default)
            Context.Data.Remove(KeyboardIdDataKey);
        else
            Context.Data[KeyboardIdDataKey] = id.ToString();
    }

    private void SetListId(long listId = default)
    {
        if (listId == default)
            Context.Data.Remove(ListIdDataKey);
        else
            Context.Data[ListIdDataKey] = listId.ToString();
    }

    private async Task AddTaskItems(params string[] tasks)
    {
        if (Context.Data.TryGetValue(ListIdDataKey, out var id_str) is false)
            Debug.Fail("Tried to add a task to a list with no set list id in Data");

        var listId = long.Parse(id_str);
        var db = Services.GetRequiredService<ToDoListDbContext>();

        for (int i = 0; i < tasks.Length; i++)
            db.ToDoTasks.Add(new ToDoListTask()
            {
                Id = Snowflake.New(),
                Name = tasks[i],
                ToDoListId = listId,
            });

        await db.SaveChangesAsync();
        Context.Data.Remove(ListIdDataKey);
    }

    private Task TaskNotFoundMessage(KeyboardResponse kr)
        => Bot.AnswerKeyboardResponse(kr, "Could not find the task; please write /cancel if you want to do something else.");

    private Task ListNotFoundMessage(KeyboardResponse kr)
        => Bot.AnswerKeyboardResponse(kr, "Could not find the list; please write /cancel if you want to do something else.");

    private Task InvalidMessage()
        => Bot.RespondWithText("I'm sorry, I couldn't understand this message. Can you try again?");

    private Task InvalidMessage(KeyboardResponse kr)
        => Bot.AnswerKeyboardResponse(kr, "I'm sorry, I couldn't understand this message. Can you try again?");
}
