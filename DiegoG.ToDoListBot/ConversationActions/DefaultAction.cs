using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Telegram;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationAction(true)]
public class DefaultAction : ConversationActionBase
{
    protected override async Task<ConversationActionEndingKind> PerformAsync(UpdateContext update)
    {
        if (update is TelegramUpdateContext tl
            && tl.Update.Type is UpdateType.Message
            && string.IsNullOrWhiteSpace(tl.Update.Message!.Text) is false)
        {
            if (await ChatBotManager.CheckIfCommand(update, Context))
                return ConversationActionEndingKind.Repeat;
        }

        return ConversationActionEndingKind.Finished;
    }
}
