using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

public class ShoutoutCommand: IBotCommand
{
    public string Name => "so";
    public CommandPermission Permission => CommandPermission.Moderator;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, $"@{ctx.Message.User.DisplayName} You need to specify a user to shoutout!", ctx.Message.Id);
            return;
        }

        string name = ctx.Arguments[0].Replace("@", "").ToLower();

        try
        {
            User user = await ctx.TwitchApiService.GetOrFetchUser(name: name);

            ShoutoutQueueService shoutoutQueue = ctx.ServiceProvider.GetRequiredService<ShoutoutQueueService>();
            shoutoutQueue.EnqueueShoutout(
                ctx.Message.BroadcasterId,
                user.Id,
                ctx.Message.Broadcaster.Username,
                isManual: true);

            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"Shoutout for @{user.DisplayName} has been queued!",
                ctx.Message.Id);
        }
        catch (Exception ex)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, $"@{ctx.Message.DisplayName} An error occurred while processing the shoutout.", ctx.Message.Id);
        }
    }
}

return new ShoutoutCommand();
