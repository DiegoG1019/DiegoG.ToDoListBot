using GLV.Shared.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ToDoListBot.Options;

[RegisterOptions]
public class BotOptions
{
    public int AppId { get; set; }
    public string? ApiHash { get; set; }
    public string? BotKey { get; set; }
}
