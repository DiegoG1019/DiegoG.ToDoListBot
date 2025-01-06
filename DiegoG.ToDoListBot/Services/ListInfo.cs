using GLV.Shared.Data.Identifiers;

namespace DiegoG.ToDoListBot.Services;

public record class TaskInfo(string? Name, Snowflake Id, bool IsCompleted, string? Description);

public class ListInfo(string? name, long id)
{
    public string? Name { get; init; } = name;
    public long Id { get; init; } = id;
}
