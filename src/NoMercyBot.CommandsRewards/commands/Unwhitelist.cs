using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Other;

public class UnwhitelistCommand : IBotCommand
{
    public string Name => "unwhitelist";
    public CommandPermission Permission => CommandPermission.Broadcaster;

    public Task Init(CommandScriptContext ctx)
    {
        return Task.CompletedTask;
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        string broadcaster = ctx.Message.Broadcaster.Username;

        if (ctx.Arguments.Length < 1)
        {
            await ctx.TwitchChatService.SendReplyAsBot(broadcaster,
                "Usage: !unwhitelist @user", ctx.Message.Id);
            return;
        }

        string targetName = ctx.Arguments[0].Replace("@", "").Trim().ToLower();

        try
        {
            NoMercyBot.Database.Models.User targetUser = await ctx.TwitchApiService.GetOrFetchUser(name: targetName);

            PermissionService permissionService = ctx.ServiceProvider.GetRequiredService<PermissionService>();
            bool removed = permissionService.RevokeOverride(targetUser.Id, ctx.DatabaseContext);

            string msg = removed
                ? $"@{targetUser.DisplayName}'s permission override has been removed."
                : $"@{targetUser.DisplayName} had no permission override.";

            await ctx.TwitchChatService.SendReplyAsBot(broadcaster, msg, ctx.Message.Id);
        }
        catch
        {
            await ctx.TwitchChatService.SendReplyAsBot(broadcaster,
                $"User \"{targetName}\" not found on Twitch.", ctx.Message.Id);
        }
    }
}

return new UnwhitelistCommand();
