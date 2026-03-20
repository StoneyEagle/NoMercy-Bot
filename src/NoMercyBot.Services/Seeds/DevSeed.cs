using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace NoMercyBot.Services.Seeds;

public static class DevSeed
{
    private class SongRecord
    {
        public string SongId { get; set; } = null!;
    }

    private class BsodRecord
    {
        public string Reason { get; set; } = null!;
    }

    private class TtsRecord
    {
        public string Message { get; set; } = null!;
    }

    private class RawChatMessageEvent
    {
        [JsonProperty("message")]
        public RawChatMessageContent? Message { get; set; }

        [JsonProperty("chatter_user_id")]
        public string? ChatterUserId { get; set; }
    }

    private class RawChatMessageContent
    {
        [JsonProperty("text")]
        public string? Text { get; set; }
    }

    public static async Task Init(this AppDbContext dbContext, IServiceScope scope)
    {
        try
        {
            // Backfill command usage records from existing chat messages
            await BackfillCommandUsage(dbContext);

            List<ChannelEvent> channelEvents = await dbContext
                .ChannelEvents.Where(e => e.Type == "channel.points.custom.reward.redemption.add")
                .ToListAsync();

            string luckyFeatherRewardId = "29c1ea38-96ff-4548-9bbf-ec0b665344c0";
            string spotifyRewardId = "e67ad9d2-dfe8-4d2f-a15e-30cfded977bd";
            string bsodRewardId = "67b5638d-e523-4b53-81d7-68812f60889e";
            string ttsRewardId = "e8168189-8d2c-41fb-b8f4-2785b083a35e";

            List<Record> existingRecords = [];

            foreach (ChannelEvent channelEvent in channelEvents)
            {
                ChannelPointsCustomRewardRedemption redemption =
                    channelEvent.Data.ToJson().FromJson<ChannelPointsCustomRewardRedemption>()
                    ?? throw new("Failed to deserialize ChannelPointsCustomRewardRedemption");

                Record redemptionRecord = new()
                {
                    UserId = redemption.UserId,
                    CreatedAt = channelEvent.CreatedAt,
                    UpdatedAt = channelEvent.UpdatedAt,
                };

                if (redemption.Reward.Id == luckyFeatherRewardId)
                {
                    redemptionRecord.RecordType = "LuckyFeather";
                    redemptionRecord.Data = "";
                }
                else if (redemption.Reward.Id == spotifyRewardId)
                {
                    redemptionRecord.RecordType = "Spotify";
                    redemptionRecord.Data = new SongRecord
                    {
                        SongId = ExtractTrackId(redemption.UserInput) ?? "unknown",
                    }.ToJson();
                }
                else if (redemption.Reward.Id == bsodRewardId)
                {
                    redemptionRecord.RecordType = "BSOD";
                    redemptionRecord.Data = new BsodRecord
                    {
                        Reason = redemption.UserInput,
                    }.ToJson();
                }
                else if (redemption.Reward.Id == ttsRewardId)
                {
                    redemptionRecord.RecordType = "TTS";
                    redemptionRecord.Data = new TtsRecord
                    {
                        Message = redemption.UserInput,
                    }.ToJson();
                }
                else
                {
                    continue;
                }

                existingRecords.Add(redemptionRecord);

                dbContext.Records.Add(redemptionRecord);
            }

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"failed to add dev seed: {ex.Message}",
                Serilog.Events.LogEventLevel.Error
            );
        }
    }

    private static async Task BackfillCommandUsage(AppDbContext dbContext)
    {
        // Check if we already have command usage records
        bool hasRecords = await dbContext.Records.AnyAsync(r => r.RecordType == "CommandUsage");

        if (hasRecords)
            return;

        Logger.Setup("Backfilling command usage records from raw channel events...");

        // Get all raw channel.chat.message events — these contain ALL messages including commands
        var rawEvents = await dbContext
            .ChannelEvents.AsNoTracking()
            .Where(e => e.Type == "channel.chat.message")
            .Select(e => new
            {
                e.Data,
                e.UserId,
                e.CreatedAt,
                e.UpdatedAt,
            })
            .ToListAsync();

        List<Record> records = [];

        foreach (var evt in rawEvents)
        {
            try
            {
                string json = evt.Data.ToJson();
                RawChatMessageEvent? parsed = json.FromJson<RawChatMessageEvent>();
                string? text = parsed?.Message?.Text;
                string? userId = parsed?.ChatterUserId ?? evt.UserId;

                if (
                    string.IsNullOrEmpty(text)
                    || !text.StartsWith("!")
                    || string.IsNullOrEmpty(userId)
                )
                    continue;

                string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string commandName = parts[0].TrimStart('!').ToLowerInvariant();
                string[] args = parts.Length > 1 ? parts[1..] : [];

                records.Add(
                    new Record
                    {
                        UserId = userId,
                        RecordType = "CommandUsage",
                        Data = new CommandUsageRecord
                        {
                            Command = commandName,
                            Arguments = args,
                        }.ToJson(),
                        CreatedAt = evt.CreatedAt,
                        UpdatedAt = evt.UpdatedAt,
                    }
                );
            }
            catch
            {
                // Skip unparseable events
            }
        }

        if (records.Count == 0)
        {
            Logger.Setup("No command usages found in channel events");
            return;
        }

        dbContext.Records.AddRange(records);
        await dbContext.SaveChangesAsync();

        Logger.Setup(
            $"Backfilled {records.Count} command usage records from {records.Select(r => r.UserId).Distinct().Count()} users"
        );
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
