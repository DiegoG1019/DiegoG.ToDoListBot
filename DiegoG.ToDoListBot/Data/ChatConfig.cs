using GLV.Shared.Data;
using GLV.Shared.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiegoG.ToDoListBot.Data;

public class ChatConfig : IDbModel<ChatConfig, long>
{
    public long ChatId { get; set; }
    public TimeSpan UtcOffset { get; set; }
    
    public ISet<ToDoList>? Lists { get; set; }
    
    long IKeyed<ChatConfig, long>.Id => this.ChatId;
    
    public static void BuildModel(DbContext context, ModelBuilder mb, EntityTypeBuilder<ChatConfig> eb)
    {
        eb.HasKey(x => x.ChatId);
    }
}