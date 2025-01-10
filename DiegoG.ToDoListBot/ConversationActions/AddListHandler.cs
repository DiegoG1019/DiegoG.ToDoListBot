using DiegoG.ToDoListBot.Data;
using DiegoG.ToDoListBot.Services;
using GLV.Shared.ChatBot;
using GLV.Shared.ChatBot.Pipeline;
using GLV.Shared.ChatBot.Telegram;
using Microsoft.EntityFrameworkCore;
using WTelegram;

namespace DiegoG.ToDoListBot.ConversationActions;

public class AddListHandler : IChatBotPipelineKeyboardHandler, IChatBotPipelineMessageHandler
{
    private PipelineContext PipelineContext;

    public async Task TryProcessKeyboardResponse(PipelineContext context, KeyboardResponse keyboard)
    {
        PipelineContext = context;
        if (keyboard.MatchDataTag(ActionConstants.AddListTag))
        {
            await PipelineContext.ActiveAction.SetResponseMessage("Please tell me the name of the list, or write /cancel if you want to do something else.", null, true);
            PipelineContext.Context.SetStep(ActionConstants.AddListStepSetName);
            context.Handled = true;
        }
    }

    public async Task TryProcessMessage(PipelineContext context, Message message)
    {
        PipelineContext = context;

        if (context.Context.Step == ActionConstants.AddListStepSetName && string.IsNullOrWhiteSpace(message.Text) is false)
        {
            await AddList(message.Text, message.MessageId);
            context.Handled = true;
        }
    }

    private async Task AddList(string name, long messageId)
    {
        var chatId = PipelineContext.Update.ConversationId.UnpackTelegramConversationId();
        var db = PipelineContext.Services.GetRequiredService<ToDoListDbContext>();
        if (string.IsNullOrWhiteSpace(name) || await db.ToDoLists.AnyAsync(x => x.Name == name && x.ChatId == chatId))
        {
            try
            {
                await PipelineContext.Bot.DeleteMessage(messageId);
            }
            catch { }

            await PipelineContext.ActiveAction.SetResponseMessage($"I can't add a list with the name '{name}'; maybe there's already a list with that name? please give me another one, or write /cancel if you want to do something else.", null, true);
        }
        else
        {
            db.ToDoLists.AddNewToDoList(chatId, name);
            await db.SaveChangesAsync();

            PipelineContext.Context.ResetState();
            try
            {
                await PipelineContext.Bot.DeleteMessage(messageId);
            }
            catch { }
            await PipelineContext.ActiveAction.SetResponseMessage(
                $"The list '{name}' has been added! What else can I do for you?", 
                ToDoListConversationHelper.ActionKeyboard, 
                true
            );
        }
    }
}
