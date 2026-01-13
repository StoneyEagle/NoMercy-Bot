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
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Spotify.Dto;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using SpotifyAPI.Web;

public class SongRecord
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<DateTime> Dates { get; set; } = [];
}

public class SongReward : IReward
{
    public Guid RewardId => Guid.Parse("e67ad9d2-dfe8-4d2f-a15e-30cfded977bd");
    public string RewardTitle => "Spotify Song Request";
    public RewardPermission Permission => RewardPermission.Everyone;
    
    private static readonly string[] _songAlreadyQueuedReplies =
    {
        "@{name}, great minds think alike... or you just don't pay attention to the queue. Either way, your points are gone! 💸",
        "Congratulations @{name}! You've successfully wasted your channel points on something that was already happening. Chef's kiss! 👨‍🍳💋",
        "Plot twist @{name}: That song is already queued up! But hey, thanks for the donation to my 'viewers who don't read' fund.",
        "Ooof @{name}, that's already in the queue bestie. RIP to your channel points - they died for nothing. 🪦",
        "Breaking news: Local viewer @{name} discovers they can't ctrl+f the song queue. Channel points lost in tragic accident.",
        "That song is already queued @{name}, but don't worry - your points went to a good cause: teaching you to read!",
        "Aww sweetie @{name}, that's already playing soon! But look on the bright side - you've contributed to my 'impatient viewers' research fund!",
        "Oh honey @{name}... bless your heart. That song is already in line, but your points? They're gone forever. Thoughts and prayers. 🙏",
        "Achievement unlocked @{name}: 'Didn't Check the Queue!' Your reward? Absolutely nothing. Your points? Also nothing.",
        "Fun fact @{name}: That song is already queued! Less fun fact: Your channel points have vanished into the void. Science!"
    };

    private const string STORAGE_KEY = "SpotifyRecords";

    public async Task Init(RewardScriptContext ctx)
    {
        // Initialize storage if it doesn't exist
        Storage? storage = await ctx.DatabaseContext.Storages
            .FirstOrDefaultAsync(s => s.Key == STORAGE_KEY);
        
        if (storage == null)
        {
            storage = new()
            {
                Key = STORAGE_KEY,
                Value = "[]"
            };
            await ctx.DatabaseContext.Storages.AddAsync(storage);
            await ctx.DatabaseContext.SaveChangesAsync();
        }
    }

    public async Task Callback(RewardScriptContext ctx)
    {
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
            // Update user song request tracking
            await UpdateUserTracking(ctx);

            // Extract track ID from Spotify URL
            string? trackId = ExtractTrackId(userInput);
            if (string.IsNullOrEmpty(trackId))
            {
                string text = $"@{ctx.UserDisplayName} Failed to add song to queue. Invalid Spotify track URL format!, your point has been refunded.";
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
            
            PlayerAddToQueueRequest queueRequest = new($"spotify:track:{trackId}");
            bool success = await spotifyService.AddToQueue(queueRequest);

            if (success)
            {
                // Get track information to display song details
                FullTrack? track = await spotifyService.GetTrack(trackId);
                string text;
                
                if (track != null && track.Artists.Any())
                {
                    text = $"@{ctx.UserDisplayName} {track.Name} by {track.Artists.First().Name} has been added to the queue! 🎶";
                }
                else
                {
                    text = $"@{ctx.UserDisplayName} Your song request has been added to the queue! 🎶";
                }
                
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

    private async Task UpdateUserTracking(RewardScriptContext ctx)
    {
        List<SongRecord> songs = await ctx.DatabaseContext.Storages
            .Where(r => r.Key == STORAGE_KEY)
            .Select(r => r.Value.FromJson<List<SongRecord>>())
            .FirstOrDefaultAsync() ?? [];

        SongRecord? existingUser = songs.FirstOrDefault(s => s.UserId == ctx.UserId);
        
        if (existingUser != null)
        {
            existingUser.Count++;
            existingUser.Dates.Add(DateTime.UtcNow);
        }
        else
        {
            SongRecord newUser = new()
            {
                UserId = ctx.UserId,
                DisplayName = ctx.UserDisplayName,
                Count = 1,
                Dates = [DateTime.UtcNow]
            };
            songs.Add(newUser);
        }

        Storage storage = await ctx.DatabaseContext.Storages
            .FirstAsync(s => s.Key == STORAGE_KEY);
        
        storage.Value = songs.ToJson();
        ctx.DatabaseContext.Storages.Update(storage);
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
}

return new SongReward();
