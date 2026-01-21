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

public class SongRecord
{
    public string SongId { get; set; } = null!;
}

public class SongReward : IReward
{
    public Guid RewardId => Guid.Parse("e67ad9d2-dfe8-4d2f-a15e-30cfded977bd");
    public string RewardTitle => "Spotify Song Request";
    public RewardPermission Permission => RewardPermission.Everyone;
    
    private static readonly string[] _songAlreadyQueuedReplies =
    {
        "@{name}, great minds think alike... or you just don't pay attention to the queue. Either way, your points are gone!",
        "Congratulations @{name}! You've successfully wasted your channel points on something that was already happening. Chef's kiss!",
        "Plot twist @{name}: That song is already queued up! But hey, thanks for the donation to my 'viewers who don't read' fund.",
        "Ooof @{name}, that's already in the queue bestie. RIP to your channel points - they died for nothing. 🪦",
        "Breaking news: Local viewer @{name} discovers they can't ctrl+f the song queue. Channel points lost in tragic accident.",
        "That song is already queued @{name}, but don't worry - your points went to a good cause: teaching you to read!",
        "Aww sweetie @{name}, that's already playing soon! But look on the bright side - you've contributed to my 'impatient viewers' research fund!",
        "Oh honey @{name}... bless your heart. That song is already in line, but your points? They're gone forever. Thoughts and prayers.",
        "Achievement unlocked @{name}: 'Didn't Check the Queue!' Your reward? Absolutely nothing. Your points? Also nothing.",
        "Fun fact @{name}: That song is already queued! Less fun fact: Your channel points have vanished into the void. Science!"
    };

    private const string STORAGE_KEY = "Spotify";

    public async Task Init(RewardScriptContext ctx)
    {
        
    }

    public async Task Callback(RewardScriptContext ctx)
    {
        if (await CheckUserBannedSongs(ctx)) return;
            
        string? userInput = ctx.UserInput?.Trim();
        
        if (string.IsNullOrEmpty(userInput) || 
            (!userInput.Contains("spotify.com") && !userInput.Contains("track/")))
        {
            await ctx.ReplyAsync(
                $"@{ctx.UserDisplayName} Failed to add song to queue. Make sure you provided a valid Spotify track URL!");
            await ctx.RefundAsync();
            return;
        }

        try
        {
            // Extract track ID from Spotify URL
            string? trackId = ExtractTrackId(userInput);
            if (string.IsNullOrEmpty(trackId))
            {
                string text = $"@{ctx.UserDisplayName} Failed to add song to queue. Invalid Spotify track URL format!, your point has been refunded.";
                await ctx.ReplyAsync(text);
                await ctx.RefundAsync();
                return;
            }
            
            // Check if the song is banned for the user
            if (await IsSongBanned(ctx, trackId))
            {
                string text = $"@{ctx.UserDisplayName} Failed to add song to queue. This song is banned, your point has been refunded.";
                await ctx.ReplyAsync(text);
                await ctx.RefundAsync();
                return;
            }

            // Add to Spotify queue
            SpotifyApiService spotifyService = ctx.ServiceProvider.GetRequiredService<SpotifyApiService>();
            
            SpotifyQueueResponse? queue = await spotifyService.GetQueue();
            if (queue != null && queue.Queue.Any(q => q.Id == trackId))
            {
                string randomTemplate = _songAlreadyQueuedReplies[Random.Shared.Next(_songAlreadyQueuedReplies.Length)];
                string text = TemplateHelper.ReplaceTemplatePlaceholders(randomTemplate, ctx);
                await ctx.ReplyAsync(text);
                await ctx.FulfillAsync();
                return;
            }
            
            FullTrack? track = await spotifyService.GetTrack(trackId);
            
            if (track == null)
            {
                string text = $"@{ctx.UserDisplayName} Failed to add song to queue. Could not retrieve track information, your point has been refunded.";
                await ctx.ReplyAsync(text);
                await ctx.RefundAsync();
                return;
            }
            
            if (track.DurationMs > 10 * 60 * 1000) // 10 minutes
            {
                string text = $"@{ctx.UserDisplayName} Failed to add song to queue. The track exceeds the maximum allowed duration of 10 minutes, your point has been refunded.";
                await ctx.ReplyAsync(text);
                await ctx.RefundAsync();
                return;
            }
            
            PlayerAddToQueueRequest queueRequest = new($"spotify:track:{trackId}");
            bool success = await spotifyService.AddToQueue(queueRequest);

            if (success)
            {
                // Get track information to display song details
                string text;
                
                if (track != null && track.Artists.Any())
                {
                    text = $"@{ctx.UserDisplayName} {track.Name} by {track.Artists.First().Name} has been added to the queue! 🎶";
                }
                else
                {
                    text = $"@{ctx.UserDisplayName} Your song request has been added to the queue! 🎶";
                }
                
                // Update user song request tracking
                await StoreRecordAsync(ctx, trackId);
                
                await ctx.ReplyAsync(text);
                await ctx.FulfillAsync();
            }
            else
            {
                string text = $"@{ctx.UserDisplayName} Failed to add song to queue. Please try again later, your point has been refunded.";
                await ctx.ReplyAsync(text);
                await ctx.RefundAsync();
            }
        }
        catch (Exception ex)
        {
            string text = $"@{ctx.UserDisplayName} An error occurred while processing your song request: {ex.Message}, your point has been refunded.";
            await ctx.ReplyAsync(text);
            await ctx.RefundAsync();
        }
    }

    private async Task StoreRecordAsync(RewardScriptContext ctx, string trackId)
    {
        SongRecord newSongRecord = new()
        {
            SongId = trackId
        };
        
        Record record = new()
        {
            UserId = ctx.UserId,
            RecordType = STORAGE_KEY,
            Data = newSongRecord.ToJson()
        };
            
        ctx.DatabaseContext.Records.Add(record);
        await ctx.DatabaseContext.SaveChangesAsync();
    }

    private static string? ExtractTrackId(string input)
    {
        // Handle both URL and URI formats
        if (input.Contains("spotify.com") && input.Contains("track/"))
        {
            // Extract from URL: https://open.spotify.com/track/4iV5W9uYEdYUVa79Axb7Rh?si=...
            string[] urlParts = input.Split('/');
            for (int i = 0; i < urlParts.Length - 1; i++)
            {
                if (urlParts[i] == "track")
                {
                    return urlParts[i + 1].Split('?')[0];
                }
            }
        }
        
        if (input.Contains("spotify:track:"))
        {
            // Extract from URI: spotify:track:4iV5W9uYEdYUVa79Axb7Rh
            return input.Split(':').LastOrDefault();
        }

        return null;
    }

    private static async Task<Boolean> IsSongBanned(RewardScriptContext ctx, string trackId)
    {
        int count = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == ctx.UserId 
                        && r.RecordType == "BannedSong" 
                        && r.Data.Contains($"\"SongId\":\"{trackId}\""))
            .CountAsync(ctx.CancellationToken);

        return count > 0;
    }
    
    private static async Task<Boolean> CheckUserBannedSongs(RewardScriptContext ctx)
    {
        int count = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == ctx.UserId && r.RecordType == "BannedSong")
            .CountAsync(ctx.CancellationToken);

        if (count >= 10)
        {
            string text = "Stop redeeming songs, your permission has been revoked";
            await ctx.TwitchChatService.SendMessageAsBot(ctx.BroadcasterLogin, text);
            
            return true;
        }
        
        return false;
    }
}

return new SongReward();
