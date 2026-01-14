using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Dto;
using NoMercyBot.Services.Twitch.Scripting;

public class FollowageCommand: IBotCommand
{
    public string Name => "followage";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        ChannelFollowersResponseData? follow = await ctx.TwitchApiService.GetChannelFollower(ctx.Message.Broadcaster.Id, ctx.Message.User.Id);

        if (follow != null)
        {
            TimeSpan followDuration = DateTimeOffset.UtcNow - follow.FollowedAt;
            string durationText = FormatDuration(followDuration);
            string text = $"@{ctx.Message.User.DisplayName} You have been following for {durationText}!";
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
        }
        else
        {
            string text = $"@{ctx.Message.User.DisplayName} You are not following!";
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
        }
    }
    
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 365)
        {
            int years = (int)(duration.TotalDays / 365);
            int remainingDays = (int)(duration.TotalDays % 365);
            return years == 1 
                ? $"{years} year" + (remainingDays > 0 ? $" and {remainingDays} days" : "")
                : $"{years} years" + (remainingDays > 0 ? $" and {remainingDays} days" : "");
        }
        else if (duration.TotalDays >= 30)
        {
            int months = (int)(duration.TotalDays / 30);
            int remainingDays = (int)(duration.TotalDays % 30);
            return months == 1 
                ? $"{months} month" + (remainingDays > 0 ? $" and {remainingDays} days" : "")
                : $"{months} months" + (remainingDays > 0 ? $" and {remainingDays} days" : "");
        }
        else if (duration.TotalDays >= 1)
        {
            int days = (int)duration.TotalDays;
            return days == 1 ? $"{days} day" : $"{days} days";
        }
        else if (duration.TotalHours >= 1)
        {
            int hours = (int)duration.TotalHours;
            return hours == 1 ? $"{hours} hour" : $"{hours} hours";
        }
        else
        {
            int minutes = Math.Max(1, (int)duration.TotalMinutes);
            return minutes == 1 ? $"{minutes} minute" : $"{minutes} minutes";
        }
    }
}

return new FollowageCommand();