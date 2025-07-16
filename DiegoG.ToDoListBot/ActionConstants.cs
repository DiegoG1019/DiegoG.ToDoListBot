namespace DiegoG.ToDoListBot;

public static class ActionConstants
{
    public const string AddListTag = "cmd:AddList";
    public const string RemoveListTag = "cmd:RemoveList";
    public const string ViewListsTag = "cmd:ViewLists";
    public const string ViewMainMenuTag = "cmd:MainMenu";
    public const string SetTimeZone = "cmd:TimeZone";

    public const string BotMessageContextKey = "ck:bot_message";

    public const int EditTimeZoneStepSetName = 5;
    public const int EditListReminderStepSetName = 4;
    public const int EditTaskStepSetName = 3;
    public const int AddListStepSetName = 1;
    public const int AddTaskStepName = 2;

    public const string DeleteListDataHeader = "Delete:ListId:";
    public const string ViewListHeader = "View:ListId:";
    public const string ViewListFromTaskHeader = "ViewList:TaskId:";
    public const string ExportListHeader = "Export:ListId:";
    public const string EditListReminder = "Reminder:ListId:";

    public const string ExportListTextHeader = "Export:Text:ListId:";

    public const string DeleteTaskHeader = "Delete:TaskId:";
    public const string ViewTaskHeader = "View:TaskId:";
    public const string EditTaskHeader = "Edit:TaskId:";
    public const string ToggleTaskHeader = "Toggle:TaskId:";
    public const string AddTaskHeader = "Add:ListId:";
}
