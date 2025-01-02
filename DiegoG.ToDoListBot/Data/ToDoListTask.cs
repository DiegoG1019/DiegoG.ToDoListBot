using GLV.Shared.Data.Identifiers;
using GLV.Shared.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiegoG.ToDoListBot.Data;

public class ToDoListTask : IDbModel<ToDoListTask, Snowflake>
{
    public Snowflake Id { get; init; }
    public long ToDoListId { get; set; }
    public ToDoList? ToDoList { get; set; }
    public string? Name { get; set; }
    public bool IsCompleted { get; set; }
    
    public static void BuildModel(DbContext context, EntityTypeBuilder<ToDoListTask> mb)
    {
        mb.HasKey(x => x.Id);
        mb.Property(x => x.Id).HasConversion(x => x.AsLong(), x => new Snowflake(x));
    }
}
