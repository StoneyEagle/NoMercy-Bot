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
        int raidDelaySeconds = 90; // Default 90 seconds

        // Parse optional delay argument
        if (ctx.Arguments.Length > 1 && int.TryParse(ctx.Arguments[1], out int customDelay))
        {
            raidDelaySeconds = Math.Clamp(customDelay, 10, 300); // Between 10 seconds and 5 minutes
        }

        try
        {
            // Fetch target user information
            User targetUser = await ctx.TwitchApiService.GetOrFetchUser(name: targetUsername);
            ChannelInfo targetChannelInfo = await ctx.TwitchApiService.GetOrFetchChannelInfo(id: targetUser.Id);

            // Check if target is live and raidable
            StreamInfo? streamInfo = await ctx.TwitchApiService.GetStreamInfo(broadcasterId: targetUser.Id);
            if (streamInfo == null)
            {
                await ctx.TwitchChatService.SendReplyAsBot(
                    ctx.Message.Broadcaster.Username,
                    $"@{ctx.Message.User.DisplayName} {targetUser.DisplayName} is not currently live. You can only raid live channels!",
                    ctx.Message.Id);
                return;
            }

            string gameName = targetChannelInfo.GameName ?? "something awesome";
            string title = targetChannelInfo.Title ?? "";

            // Create modified context for template replacement
            CommandScriptContext modifiedCtx = new CommandScriptContext
            {
                Message = new()
                {
                    UserId = targetUser.Id,
                    Username = targetUser.Username,
                    DisplayName = targetUser.DisplayName,
                    User = targetUser,
                },
                Channel = ctx.Message.Broadcaster.Username,
                BroadcasterId = ctx.BroadcasterId,
                CommandName = ctx.CommandName,
                Arguments = ctx.Arguments,
                ReplyAsync = ctx.ReplyAsync,
                CancellationToken = ctx.CancellationToken,
                ServiceProvider = ctx.ServiceProvider,
                TwitchChatService = ctx.TwitchChatService,
                TtsService = ctx.TtsService,
                TwitchApiService = ctx.TwitchApiService,
                DatabaseContext = ctx.DatabaseContext,
            };

            // Send raid announcement with copy-pasteable messages
            await ctx.TwitchChatService.SendMessageAsBot(
                ctx.Message.Broadcaster.Username,
                $"RAID INCOMING to {targetUser.DisplayName}! Starting in {raidDelaySeconds} seconds...");

            await Task.Delay(1000); // Small delay between messages

            await ctx.TwitchChatService.SendMessageAsBot(
                ctx.Message.Broadcaster.Username,
                "Big bird raid stoney90Hmmm");

            await Task.Delay(500);

            await ctx.TwitchChatService.SendMessageAsBot(
                ctx.Message.Broadcaster.Username,
                "Big bird raid 🦅");

            // Get OBS service and switch to ending scene
            try
            {
                ObsApiService obsService = (ObsApiService)ctx.ServiceProvider.GetService(typeof(ObsApiService));
                if (obsService != null)
                {
                    try
                    {
                        await obsService.SetCurrentScene("Ending");
                        Logger.Twitch("Switched OBS scene to 'ending'", LogEventLevel.Information);
                    }
                    catch (Exception obsEx)
                    {
                        Logger.Twitch($"Could not switch OBS scene (scene 'ending' may not exist): {obsEx.Message}", LogEventLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Twitch($"OBS service not available: {ex.Message}", LogEventLevel.Warning);
            }

            // Countdown timer
            int[] countdownTimes = { 60, 30, 10, 5, 3, 2, 1 };
            foreach (int timeLeft in countdownTimes)
            {
                if (timeLeft >= raidDelaySeconds) continue;

                int waitTime = raidDelaySeconds - timeLeft;
                if (waitTime > 0)
                {
                    await Task.Delay(waitTime * 1000, ctx.CancellationToken);
                    raidDelaySeconds = timeLeft;
                }

                if (timeLeft <= 10)
                {
                    await ctx.TwitchChatService.SendMessageAsBot(
                        ctx.Message.Broadcaster.Username,
                        $"Raid in {timeLeft} second{(timeLeft != 1 ? "s" : "")}...");
                }
            }

            // Final wait to reach 0
            if (raidDelaySeconds > 0)
            {
                await Task.Delay(raidDelaySeconds * 1000, ctx.CancellationToken);
            }

            // Execute the raid
            try
            {
                await ctx.TwitchApiService.RaidAsync(ctx.BroadcasterId, targetUser.Id);
                Logger.Twitch($"Successfully raided {targetUser.DisplayName}", LogEventLevel.Information);

                await ctx.TwitchChatService.SendMessageAsBot(
                    ctx.Message.Broadcaster.Username,
                    $"RAID LIVE! We're heading to {targetUser.DisplayName}! Let's go!");
            }
            catch (Exception raidEx)
            {
                Logger.Twitch($"Failed to execute raid: {raidEx.Message}", LogEventLevel.Error);
                await ctx.TwitchChatService.SendMessageAsBot(
                    ctx.Message.Broadcaster.Username,
                    $"Raid command failed. Please manually raid {targetUser.DisplayName}!");
            }

            // Wait a moment for raid to process
            await Task.Delay(3000, ctx.CancellationToken);

            // Stop the stream
            try
            {
                ObsApiService obsService = (ObsApiService)ctx.ServiceProvider.GetService(typeof(ObsApiService));
                if (obsService != null)
                {
                    await obsService.StopStreaming();
                    Logger.Twitch("Stopped OBS stream", LogEventLevel.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Twitch($"Could not stop OBS stream: {ex.Message}", LogEventLevel.Warning);
            }

            // Pause Spotify
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
        catch (Exception ex)
        {
            Logger.Twitch($"Error in raid command: {ex.Message}", LogEventLevel.Error);
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.User.DisplayName} An error occurred while setting up the raid. Please try again or raid manually!",
                ctx.Message.Id);
        }
    }
}

return new RaidCommand();
