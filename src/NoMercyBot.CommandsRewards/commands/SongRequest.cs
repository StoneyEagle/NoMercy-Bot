using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Spotify.Dto;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using SpotifyAPI.Web;

public class SrSongRecord
{
    public string SongId { get; set; } = null!;
}

public class SongRequestCommand : IBotCommand
{
    public string Name => "sr";
    public CommandPermission Permission => CommandPermission.Everyone;

    private static readonly string[] _songAlreadyQueuedReplies =
    {
        "@{name}, great minds think alike... or you just don't pay attention to the queue. Either way, that song is already queued!",
        "Congratulations @{name}! That song is already in the queue. Maybe check next time?",
        "Plot twist @{name}: That song is already queued up!",
        "Ooof @{name}, that's already in the queue bestie.",
        "Breaking news: Local viewer @{name} discovers they can't ctrl+f the song queue.",
        "That song is already queued @{name}, but don't worry - now you know!",
        "Aww sweetie @{name}, that's already playing soon!",
        "Oh honey @{name}... bless your heart. That song is already in line.",
        "Achievement unlocked @{name}: 'Didn't Check the Queue!'",
        "Fun fact @{name}: That song is already queued! Less fun fact: You didn't check first."
    };

    private const string STORAGE_KEY = "Spotify";

    public Task Init(CommandScriptContext ctx)
    {
        return Task.CompletedTask;
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        if (await CheckUserBannedSongs(ctx)) return;

        string userInput = string.Join(" ", ctx.Arguments).Trim();
        string displayName = ctx.Message.DisplayName;
        string userId = ctx.Message.UserId;
        string broadcasterLogin = ctx.Message.Broadcaster.Username;

        if (string.IsNullOrEmpty(userInput))
        {
            await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                $"@{displayName} Usage: !sr <spotify url or song name>", ctx.Message.Id);
            return;
        }

        try
        {
            SpotifyApiService spotifyService = ctx.ServiceProvider.GetRequiredService<SpotifyApiService>();

            string? type;
            string? trackId;
            bool isUrl = userInput.Contains("spotify.com") || userInput.Contains("spotify:");

            if (isUrl)
            {
                (type, trackId) = ExtractTrackId(userInput) ?? (null, null);
                if (string.IsNullOrEmpty(trackId) || string.IsNullOrEmpty(type))
                {
                    await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                        $"@{displayName} Invalid Spotify URL format!", ctx.Message.Id);
                    return;
                }
            }
            else
            {
                FullTrack? searchResult = await SearchTrack(spotifyService, userInput);
                if (searchResult == null)
                {
                    await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                        $"@{displayName} No results found for \"{userInput}\".", ctx.Message.Id);
                    return;
                }
                type = "track";
                trackId = searchResult.Id;
            }

            if (await IsSongBanned(ctx, userId, trackId))
            {
                await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                    $"@{displayName} Failed to add song to queue. This song is banned.", ctx.Message.Id);
                return;
            }

            SpotifyQueueResponse? queue = await spotifyService.GetQueue();
            if (queue != null && queue.Queue.Any(q => q.Id == trackId))
            {
                string randomTemplate = _songAlreadyQueuedReplies[Random.Shared.Next(_songAlreadyQueuedReplies.Length)];
                string text = randomTemplate.Replace("{name}", displayName);
                await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin, text, ctx.Message.Id);
                return;
            }

            FullTrack? track = null;
            FullEpisode? episode = null;
            int durationMs;
            string itemName;
            string itemArtist;

            if (type == "track")
            {
                track = await spotifyService.GetTrack(trackId);
                if (track == null)
                {
                    await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                        $"@{displayName} Failed to add song to queue. Could not retrieve track information.", ctx.Message.Id);
                    return;
                }
                durationMs = track.DurationMs;
                itemName = track.Name;
                itemArtist = track.Artists.FirstOrDefault()?.Name ?? string.Empty;
            }
            else
            {
                episode = await spotifyService.GetEpisode(trackId);
                if (episode == null)
                {
                    await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                        $"@{displayName} Failed to add episode to queue. Could not retrieve episode information.", ctx.Message.Id);
                    return;
                }
                durationMs = episode.DurationMs;
                itemName = episode.Name;
                itemArtist = episode.Show?.Name ?? string.Empty;
            }

            if (durationMs > 10 * 60 * 1000)
            {
                await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                    $"@{displayName} Failed to add to queue. The {type} exceeds the maximum allowed duration of 10 minutes.", ctx.Message.Id);
                return;
            }

            PlayerAddToQueueRequest queueRequest = new($"spotify:{type}:{trackId}");
            bool success = await spotifyService.AddToQueue(queueRequest);

            if (success)
            {
                string text = !string.IsNullOrEmpty(itemArtist)
                    ? $"@{displayName} {itemName} by {itemArtist} has been added to the queue!"
                    : $"@{displayName} {itemName} has been added to the queue!";

                await StoreRecordAsync(ctx, userId, trackId);
                await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin, text, ctx.Message.Id);
            }
            else
            {
                await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                    $"@{displayName} Failed to add to queue. Please try again later.", ctx.Message.Id);
            }
        }
        catch (Exception ex)
        {
            await ctx.TwitchChatService.SendReplyAsBot(broadcasterLogin,
                $"@{displayName} An error occurred while processing your song request: {ex.Message}", ctx.Message.Id);
        }
    }

    private static async Task<FullTrack?> SearchTrack(SpotifyApiService spotifyService, string query)
    {
        SpotifyClient client = new(spotifyService.Service.AccessToken);
        SearchResponse result = await client.Search.Item(new SearchRequest(SearchRequest.Types.Track, query)
        {
            Limit = 1
        });
        return result.Tracks?.Items?.FirstOrDefault();
    }

    private async Task StoreRecordAsync(CommandScriptContext ctx, string userId, string trackId)
    {
        SrSongRecord newSongRecord = new()
        {
            SongId = trackId
        };

        Record record = new()
        {
            UserId = userId,
            RecordType = STORAGE_KEY,
            Data = newSongRecord.ToJson()
        };

        ctx.DatabaseContext.Records.Add(record);
        await ctx.DatabaseContext.SaveChangesAsync();
    }

    private static (string, string)? ExtractTrackId(string input)
    {
        string? id = null;
        string? type = null;

        if (input.Contains("spotify.com") && (input.Contains("track/") || input.Contains("episode/")))
        {
            string[] urlParts = input.Split('/');
            for (int i = 0; i < urlParts.Length - 1; i++)
            {
                if (urlParts[i] == "track" || urlParts[i] == "episode")
                {
                    type = urlParts[i];
                    id = urlParts[i + 1].Split('?')[0];
                    break;
                }
            }
        }

        if (input.Contains("spotify:track:"))
        {
            type = "track";
            id = input.Split(':').LastOrDefault();
        }

        if (input.Contains("spotify:episode:"))
        {
            type = "episode";
            id = input.Split(':').LastOrDefault();
        }

        return string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type) ? null : (type, id);
    }

    private static async Task<bool> IsSongBanned(CommandScriptContext ctx, string userId, string trackId)
    {
        int count = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == userId
                        && r.RecordType == "BannedSong"
                        && r.Data.Contains($"\"SongId\":\"{trackId}\""))
            .CountAsync(ctx.CancellationToken);

        return count > 0;
    }

    private static async Task<bool> CheckUserBannedSongs(CommandScriptContext ctx)
    {
        string userId = ctx.Message.UserId;
        int count = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == userId && r.RecordType == "BannedSong")
            .CountAsync(ctx.CancellationToken);

        if (count >= 10)
        {
            await ctx.TwitchChatService.SendMessageAsBot(ctx.Message.Broadcaster.Username,
                "Stop requesting songs, your permission has been revoked");
            return true;
        }

        return false;
    }
}

return new SongRequestCommand();
