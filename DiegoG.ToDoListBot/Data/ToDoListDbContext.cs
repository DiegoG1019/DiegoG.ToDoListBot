using GLV.Shared.ChatBot.EntityFramework;
using GLV.Shared.EntityFramework;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ToDoListBot.Data;
public class ToDoListDbContext(DbContextOptions<ToDoListDbContext> options) : DbContext(options)
{
    public DbSet<ToDoList> ToDoLists => Set<ToDoList>();
    public DbSet<ToDoListTask> ToDoTasks => Set<ToDoListTask>();
    public DbSet<ConversationContextPacked> ConversationContexts => Set<ConversationContextPacked>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        this.BuildModelWithIDbModel(modelBuilder);
    }
}
