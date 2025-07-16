using DiegoG.ToDoListBot.ConversationActions.PipelineHandlers;
using GLV.Shared.ChatBot;
using GLV.Shared.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiegoG.ToDoListBot.Data;
using GLV.Shared.ChatBot.Telegram;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types.Enums;
using WTelegram;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationActionPipelineHandler(typeof(ExportListHandler))]
[ConversationActionPipelineHandler(typeof(TaskHandler))]
[ConversationActionPipelineHandler(typeof(ViewListHandler))]
[ConversationActionPipelineHandler(typeof(RemoveListHandler))]
[ConversationActionPipelineHandler(typeof(AddListHandler))]
[ConversationActionPipelineHandler(typeof(EditListReminderHandler))]
[ConversationActionPipelineHandler(typeof(TimeZoneHandler))]
[ConversationAction(true)]
public class DefaultAction : ConversationActionBase
{
    protected override async Task PerformAsync(UpdateContext update)
    {
        var db = Services.GetRequiredService<ToDoListDbContext>();
        var cid = update.ConversationId.UnpackTelegramConversationId();
        var cc = await db.ChatConfigs.SingleOrDefaultAsync(x => x.ChatId == cid);
        if (cc is null)
        {
            cc = new()
            {
                ChatId = cid,
            };
            db.ChatConfigs.Add(cc);
            await db.SaveChangesAsync();
        }
        update.AddOrReplaceFeature(cc);
        
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
