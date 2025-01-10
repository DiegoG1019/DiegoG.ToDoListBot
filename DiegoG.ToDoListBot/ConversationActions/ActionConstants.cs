namespace DiegoG.ToDoListBot.ConversationActions;

public static class ActionConstants
{
    public const string AddListTag = "cmd:AddList";
    public const string RemoveListTag = "cmd:RemoveList";
    public const string ViewListsTag = "cmd:ViewLists";

    public const string BotMessageContextKey = "ck:bot_message";

    public const int AddListStepSetName = 1;
    public const int AddTaskStepName = 2;

    public const string DeleteListDataHeader = "Delete:ListId:";
    public const string ViewListHeader = "View:ListId:";

    public const string DeleteTaskHeader = "Delete:TaskId:";
    public const string ViewTaskHeader = "View:TaskId:";
    public const string EditTaskHeader = "Edit:TaskId:";
    public const string ToggleTaskHeader = "Toggle:TaskId:";
    public const string AddTaskHeader = "Add:ListId:";
}
