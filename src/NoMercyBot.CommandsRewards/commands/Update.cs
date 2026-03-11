using System;
using System.Linq;
using System.Threading.Tasks;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using Serilog.Events;

public class UpdateCommand : IBotCommand
{
    public string Name => "update";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx) { }

    public async Task Callback(CommandScriptContext ctx)
    {
        try
        {
            string targetUsername;
            bool isSelfUpdate;

            if (ctx.Arguments.Length == 0)
            {
                targetUsername = ctx.Message.Username;
                isSelfUpdate = true;
            }
            else
            {
                targetUsername = ctx.Arguments[0].Replace("@", "").ToLower();
                isSelfUpdate = string.Equals(targetUsername, ctx.Message.Username, StringComparison.OrdinalIgnoreCase);
            }

            if (!isSelfUpdate)
            {
                bool isMod = ctx.Message.UserType == "Moderator"
                    || ctx.Message.UserType == "LeadModerator"
                    || ctx.Message.UserType == "Broadcaster"
                    || ctx.Message.UserType == "Staff";
                if (!isMod)
                {
                    await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                        "@" + ctx.Message.DisplayName + " You can only update your own info, or be a mod to update others.",
                        ctx.Message.Id);
                    return;
                }
            }

            User updatedUser = await ctx.TwitchApiService.FetchUser(login: targetUsername);

            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "Updated user info for " + updatedUser.DisplayName + "!",
                ctx.Message.Id);
        }
        catch (Exception ex)
        {
            Logger.System("Update command error: " + ex.Message + "\n" + ex.StackTrace, Serilog.Events.LogEventLevel.Error);
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "@" + ctx.Message.DisplayName + " Something went wrong updating the user.",
                ctx.Message.Id);
        }
    }
}

return new UpdateCommand();
