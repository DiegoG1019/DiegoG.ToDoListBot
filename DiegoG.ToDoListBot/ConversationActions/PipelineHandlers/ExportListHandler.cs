using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Pipeline;
using GLV.Shared.ChatBot.Telegram;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ToDoListBot.ConversationActions.PipelineHandlers;

public class ExportListHandler : IChatBotPipelineKeyboardHandler
{
    public async Task TryProcessKeyboardResponse(PipelineContext context, KeyboardResponse keyboard)
    {
        if (keyboard.TryGetIdFromData(ActionConstants.ExportListTextHeader, out long listid))
        {
            var chatid = context.Context.ConversationId.UnpackTelegramConversationId();
            var todolist = await context.Services.GetRequiredService<ToDoListDbContext>()
                                        .ToDoLists.Include(x => x.Tasks).FirstOrDefaultAsync(x => x.ChatId == chatid && x.Id == listid);
            if (todolist is null)
            {
                await context.ActiveAction.SetResponseMessage(
                    "Could not export the list, I couldn't find it. Maybe it was deleted?",
                    ToDoListKeyboards.ActionKeyboard,
                    true
                );
            }
            else
            {
                if (todolist.Tasks?.Any() is not true)
                    await context.Bot.SendMessage($"<u>{todolist.Name}</u>\n\n<i>There are no tasks in this list...</i>", html: true);
                else
                {
                    StringBuilder sb = new(todolist.Tasks.Count * 100);
                    foreach (var task in todolist.Tasks)
                        sb.AppendLine(task.Name);

                    await context.Bot.SendMessage($"<u>{todolist.Name}</u>\n\n{sb}", html: true);
                }

                await context.ActiveAction.SetResponseMessage(
                    "Succesfully exported! See the message above!",
                    await context.ActiveAction.GetListItems(listid) ?? ToDoListKeyboards.ActionKeyboard,
                    true
                );
            }

            context.MarkAsHandled();
        }
    }
}
