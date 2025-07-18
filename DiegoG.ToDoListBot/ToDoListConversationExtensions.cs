﻿using GLV.Shared.ChatBot;
using System.Diagnostics;
using Cronos;

namespace DiegoG.ToDoListBot;

public static class ToDoListConversationExtensions
{
    public static async Task SetResponseMessage(
        this ConversationActionBase action, 
        string? text, 
        Keyboard? kr = null, 
        bool deleteMessage = false,
        MessageOptions options = default
    )
    {
        if (action.Context.Data.TryGetValue<long>(ActionConstants.BotMessageContextKey, out var mid) is true)
        {
            try
            {
                if (deleteMessage)
                    await action.Bot.DeleteMessage(mid);
                else
                {
                    await action.Bot.EditMessage(mid, text, kr, options);
                    return;
                }
            }
            catch (Exception e)
            {
                action.SinkLogMessage(2, "An unexpected exception was thrown", -1272357452, e);
            }
        }

        action.Context.Data.Set(ActionConstants.BotMessageContextKey, await action.Bot.SendMessage(text, kr, options: options));
    }

    public static bool TryGetCronNextReminder(this string? cron, TimeSpan utcOffset, out DateTimeOffset nextReminder)
    {
        if (string.IsNullOrWhiteSpace(cron) is false && CronExpression.TryParse(cron, out var expr))
        {
            var rmd = expr.GetNextOccurrence(DateTime.UtcNow + utcOffset);
            if (rmd is DateTime dt)
            {
                nextReminder = new(new DateTime(DateOnly.FromDateTime(dt), TimeOnly.FromDateTime(dt), DateTimeKind.Unspecified), utcOffset);
                return true;
            }
        }

        nextReminder = default;
        return false;
    }

    public static bool TryGetIdFromData(this KeyboardResponse kr, string header, out long id)
    {
        Debug.Assert(kr.Data is not null);
        if (kr.Data.StartsWith(header))
        {
            id = long.Parse(kr.Data.AsSpan()[header.Length..]);
            return true;
        }

        id = default;
        return false;
    }
}
