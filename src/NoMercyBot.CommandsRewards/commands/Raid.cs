using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Dto;
using NoMercyBot.Services.Obs;
using NoMercyBot.Services.Spotify;
using Serilog.Events;
using System.Threading;

public class RaidCommand: IBotCommand
{
    public string Name => "raid";
    public CommandPermission Permission => CommandPermission.Broadcaster;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.User.DisplayName} You need to specify a channel to raid! Usage: !raid <username>",
                ctx.Message.Id);
            return;
        }

        string targetUsername = ctx.Arguments[0].Replace("@", "").ToLower();
        int raidDelaySeconds = 60; // Default 60 seconds

        // Parse optional delay argument
        if (ctx.Arguments.Length > 1 && int.TryParse(ctx.Arguments[1], out int customDelay))
        {
            raidDelaySeconds = Math.Clamp(customDelay, 10, 300); // Between 10 seconds and 5 minutes
        }

        try
        {
            User targetUser = await ctx.TwitchApiService.GetOrFetchUser(name: targetUsername);
            ChannelInfo targetChannelInfo = await ctx.TwitchApiService.GetOrFetchChannelInfo(id: targetUser.Id);

            StreamInfo? streamInfo = await ctx.TwitchApiService.GetStreamInfo(broadcasterId: targetUser.Id);
            if (streamInfo == null)
            {
                await ctx.TwitchChatService.SendReplyAsBot(
                    ctx.Message.Broadcaster.Username,
                    $"@{ctx.Message.User.DisplayName} {targetUser.DisplayName} is not currently live. You can only raid live channels!",
                    ctx.Message.Id);
                return;
            }

            await SwitchToEndingScene(ctx);
            await InitializeRaid(ctx, targetUser);
            await AnnounceRaid(ctx, targetUser, raidDelaySeconds);
            await RunCountdown(ctx, Math.Max(raidDelaySeconds - 3, 1));
            await CommitRaid(ctx, targetUser);
            await PauseSpotify(ctx);
        }
        catch (Exception ex)
        {
            Logger.Twitch($"Error in raid command: {ex.Message}", LogEventLevel.Error);
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.User.DisplayName} An error occurred while setting up the raid. Please try again or raid manually!",
                ctx.Message.Id);
        }
    }

    private async Task SwitchToEndingScene(CommandScriptContext ctx)
    {
        try
        {
            ObsApiService obsService = (ObsApiService)ctx.ServiceProvider.GetService(typeof(ObsApiService));
            if (obsService != null)
            {
                await obsService.SetCurrentScene("Ending");
                Logger.Twitch("Switched OBS scene to 'Ending'", LogEventLevel.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.Twitch($"Could not switch OBS scene: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private async Task InitializeRaid(CommandScriptContext ctx, User targetUser)
    {
        await ctx.TwitchApiService.RaidAsync(ctx.BroadcasterId, targetUser.Id);
        Logger.Twitch($"Raid initialized to {targetUser.DisplayName}", LogEventLevel.Information);
    }

    private async Task AnnounceRaid(CommandScriptContext ctx, User targetUser, int raidDelaySeconds)
    {
        await ctx.TwitchChatService.SendMessageAsBot(
            ctx.Message.Broadcaster.Username,
            $"RAID INCOMING to {targetUser.DisplayName}! Raiding in {raidDelaySeconds} seconds...");

        await Task.Delay(1000);

        await ctx.TwitchChatService.SendMessageAsBot(
            ctx.Message.Broadcaster.Username,
            "Big bird raid stoney90Hmmm Big bird raid stoney90Hmmm Big bird raid stoney90Hmmm Big bird raid stoney90Hmmm");

        await Task.Delay(500);

        await ctx.TwitchChatService.SendMessageAsBot(
            ctx.Message.Broadcaster.Username,
            "Big bird raid 🦅 Big bird raid 🦅 Big bird raid 🦅 Big bird raid 🦅 Big bird raid 🦅");
    }

    private async Task RunCountdown(CommandScriptContext ctx, int raidDelaySeconds)
    {
        int[] countdownTimes = { 45, 30, 15, 10, 5, 3, 2, 1 };
        foreach (int timeLeft in countdownTimes)
        {
            if (timeLeft >= raidDelaySeconds) continue;

            int waitTime = raidDelaySeconds - timeLeft;
            if (waitTime > 0)
            {
                await Task.Delay(waitTime * 1000, ctx.CancellationToken);
                raidDelaySeconds = timeLeft;
            }

            if (timeLeft <= 15)
            {
                await ctx.TwitchChatService.SendMessageAsBot(
                    ctx.Message.Broadcaster.Username,
                    $"Raid in {timeLeft} second{(timeLeft != 1 ? "s" : "")}...");
            }
        }

        if (raidDelaySeconds > 0)
        {
            await Task.Delay(raidDelaySeconds * 1000, ctx.CancellationToken);
        }
    }

    private async Task CommitRaid(CommandScriptContext ctx, User targetUser)
    {
        await ctx.TwitchChatService.SendMessageAsBot(
            ctx.Message.Broadcaster.Username,
            $"RAID LIVE! We're heading to {targetUser.DisplayName}! Let's go!");

        try
        {
            ObsApiService obsService = (ObsApiService)ctx.ServiceProvider.GetService(typeof(ObsApiService));
            if (obsService != null)
            {
                await obsService.StopStreaming();
                Logger.Twitch("Stopped OBS stream - raid committed", LogEventLevel.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.Twitch($"Could not stop OBS stream: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private async Task PauseSpotify(CommandScriptContext ctx)
    {
        try
        {
            SpotifyApiService spotifyService = (SpotifyApiService)ctx.ServiceProvider.GetService(typeof(SpotifyApiService));
            if (spotifyService != null)
            {
                await spotifyService.Pause();
                Logger.Twitch("Paused Spotify playback", LogEventLevel.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.Twitch($"Could not pause Spotify: {ex.Message}", LogEventLevel.Warning);
        }
    }
}

return new RaidCommand();
