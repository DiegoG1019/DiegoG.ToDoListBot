using GLV.Shared.ChatBot;

namespace DiegoG.ToDoListBot.ConversationActions;

[ConversationAction(nameof(SummonCommand), "summon", "Summons the bot")]
public class SummonCommand : ConversationActionBase
{
    protected override async Task PerformAsync(UpdateContext update)
    {
        await this.SetResponseMessage("What can I do for you?", ToDoListKeyboards.ActionKeyboard, true);
        Context.ResetState();
    }
}
