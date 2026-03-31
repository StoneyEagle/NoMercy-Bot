using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Other;

public class WhitelistCommand : IBotCommand
{
    public string Name => "whitelist";
    public CommandPermission Permission => CommandPermission.Broadcaster;

    private static readonly string[] _validLevels = { "subscriber", "vip", "moderator" };

    public Task Init(CommandScriptContext ctx)
    {
        return Task.CompletedTask;
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        string broadcaster = ctx.Message.Broadcaster.Username;

        if (ctx.Arguments.Length < 2)
        {
            await ctx.TwitchChatService.SendReplyAsBot(broadcaster,
                "Usage: !whitelist @user subscriber/vip/moderator", ctx.Message.Id);
            return;
        }

        string targetName = ctx.Arguments[0].Replace("@", "").Trim().ToLower();
        string level = ctx.Arguments[1].Trim().ToLower();

        if (!_validLevels.Contains(level))
        {
            await ctx.TwitchChatService.SendReplyAsBot(broadcaster,
                $"Invalid level \"{level}\". Valid levels: subscriber, vip, moderator", ctx.Message.Id);
            return;
        }

        // Map to the cased UserType format used internally
        string userTypeLevel = level switch
        {
            "subscriber" => "Subscriber",
            "vip" => "Vip",
            "moderator" => "Moderator",
            _ => "Subscriber"
        };

        try
        {
            NoMercyBot.Database.Models.User targetUser = await ctx.TwitchApiService.GetOrFetchUser(name: targetName);

            PermissionService permissionService = ctx.ServiceProvider.GetRequiredService<PermissionService>();
            permissionService.GrantOverride(targetUser.Id, userTypeLevel, ctx.DatabaseContext);

            await ctx.TwitchChatService.SendReplyAsBot(broadcaster,
                $"@{targetUser.DisplayName} has been granted {level} level access.", ctx.Message.Id);
        }
        catch
        {
            await ctx.TwitchChatService.SendReplyAsBot(broadcaster,
                $"User \"{targetName}\" not found on Twitch.", ctx.Message.Id);
        }
    }
}

return new WhitelistCommand();
