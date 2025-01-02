using DiegoG.ToDoListBot.Data;
using GLV.Shared.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ToDoListBot.Services;

public static class ToDoListHelpers
{
    public static void AddNewToDoList(this DbSet<ToDoList> set, long chatId, string name)
    {
        set.Add(new ToDoList()
        {
            ChatId = chatId,
            Name = name
        });
    }
}
