using GLV.Shared.Data.JsonConverters;
using GLV.Shared.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cronos;
using Telegram.Bot.Types;

namespace DiegoG.ToDoListBot.Data;

public class ToDoList : IDbModel<ToDoList, long>
{
    public long Id { get; init; }
    public long ChatId { get; set; }
    public string? Name { get; set; }
    public string? CronExpression { get; set; }
    public ISet<ToDoListTask>? Tasks { get; set; }

    public ChatConfig? ChatConfig { get; set; }
    
    private CronExpression _expr;
    public bool TryGetNextReminder(out DateTimeOffset reminder)
    {
        Debug.Assert(ChatConfig != null);
        if (string.IsNullOrWhiteSpace(CronExpression) is false)
        {
            _expr ??= Cronos.CronExpression.Parse(CronExpression);
            var rem = _expr.GetNextOccurrence(DateTime.UtcNow + ChatConfig.UtcOffset);
            if (rem is DateTime dt)
            {
                reminder = new(new DateTime(DateOnly.FromDateTime(dt), TimeOnly.FromDateTime(dt), DateTimeKind.Unspecified), ChatConfig.UtcOffset);
                return true;
            }
        }

        reminder = default;
        return false;
    }

    public static void BuildModel(DbContext context, EntityTypeBuilder<ToDoList> mb)
    {
        mb.HasKey(x => x.Id);
        mb.Property(x => x.Id).ValueGeneratedOnAdd();
        mb.HasMany(x => x.Tasks).WithOne(x => x.ToDoList).HasForeignKey(x => x.ToDoListId);
        mb.HasOne(x => x.ChatConfig).WithMany(x => x.Lists).HasForeignKey(x => x.ChatId);
        mb.Navigation(x => x.ChatConfig).AutoInclude(true);
    }
}
