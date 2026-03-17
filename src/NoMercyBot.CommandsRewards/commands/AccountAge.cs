using System;
using System.Linq;
using System.Threading.Tasks;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;
using NoMercyBot.Services.Twitch.Scripting;

public class AccountAgeCommand : IBotCommand
{
    public string Name => "accountage";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx) { }

    public async Task Callback(CommandScriptContext ctx)
    {
        var users = await ctx.TwitchApiService.GetUsers(userId: ctx.Message.User.Id);

        if (users is null || users.Count == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.User.DisplayName} Could not retrieve account information.",
                ctx.Message.Id
            );
            return;
        }

        UserInfo userInfo = users.First();
        DateTime createdAt = userInfo.CreatedAt;
        TimeSpan age = DateTime.UtcNow - createdAt;
        string durationText = FormatDuration(age);
        string dateText = createdAt.ToString("MMMM d, yyyy");

        string text =
            $"@{ctx.Message.User.DisplayName} Your account was created on {dateText} ({durationText} ago).";
        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username,
            text,
            ctx.Message.Id
        );
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 365)
        {
            int years = (int)(duration.TotalDays / 365);
            int remainingDays = (int)(duration.TotalDays % 365);
            int months = remainingDays / 30;
            int days = remainingDays % 30;

            string result = years == 1 ? "1 year" : $"{years} years";
            if (months > 0)
                result += months == 1 ? ", 1 month" : $", {months} months";
            if (days > 0)
                result += days == 1 ? ", 1 day" : $", {days} days";
            return result;
        }
        else if (duration.TotalDays >= 30)
        {
            int months = (int)(duration.TotalDays / 30);
            int days = (int)(duration.TotalDays % 30);
            string result = months == 1 ? "1 month" : $"{months} months";
            if (days > 0)
                result += days == 1 ? ", 1 day" : $", {days} days";
            return result;
        }
        else if (duration.TotalDays >= 1)
        {
            int days = (int)duration.TotalDays;
            return days == 1 ? "1 day" : $"{days} days";
        }
        else if (duration.TotalHours >= 1)
        {
            int hours = (int)duration.TotalHours;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }
        else
        {
            int minutes = Math.Max(1, (int)duration.TotalMinutes);
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }
    }
}

return new AccountAgeCommand();
