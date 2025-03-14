using DiegoG.ToDoListBot.ConversationActions.PipelineHandlers;
using GLV.Shared.ChatBot;
using GLV.Shared.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using WTelegram;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationActionPipelineHandler(typeof(ExportListHandler))]
[ConversationActionPipelineHandler(typeof(TaskHandler))]
[ConversationActionPipelineHandler(typeof(ViewListHandler))]
[ConversationActionPipelineHandler(typeof(RemoveListHandler))]
[ConversationActionPipelineHandler(typeof(AddListHandler))]
[ConversationAction(true)]
public class DefaultAction : ConversationActionBase
{
    protected override async Task PerformAsync(UpdateContext update)
    {
        if (ChatBotManager.CheckForCancellation(update, Context))
        {
            await this.SetResponseMessage("The action has been canceled. Please call me when you need me.", null, true);
        }

        if (update.Message is Message msg && string.IsNullOrWhiteSpace(msg.Text) is false)
        {
            if (Bot.IsReferringToBot(msg.Text))
            {
                await this.SetResponseMessage("What can I do for you?", ToDoListKeyboards.ActionKeyboard, true);
            }
        }

        await ExecuteActionPipeline();
    }
}
