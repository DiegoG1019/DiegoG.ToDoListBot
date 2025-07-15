using GLV.Shared.Data.JsonConverters;
using GLV.Shared.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cronos;

namespace DiegoG.ToDoListBot.Data;

public class ToDoList : IDbModel<ToDoList, long>
{
    public long Id { get; init; }
    public long ChatId { get; set; }
    public string? Name { get; set; }
    public string? CronExpression { get; set; }
    public ISet<ToDoListTask>? Tasks { get; set; }

    private CronExpression _expr;
    public bool TryGetNextReminder(out DateTimeOffset reminder)
    {
        if (string.IsNullOrWhiteSpace(CronExpression) is false)
        {
            _expr ??= Cronos.CronExpression.Parse(CronExpression);
            var rem = _expr.GetNextOccurrence(DateTime.UtcNow);
            if (rem is DateTime dt)
            {
                reminder = dt;
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
    }
}
