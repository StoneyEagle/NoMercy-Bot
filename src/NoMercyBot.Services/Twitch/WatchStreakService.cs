// TODO: Remove this service once Twitch adds watch streak support to EventSub.
// Tracking issue: https://discuss.dev.twitch.com/t/watch-streaks-via-eventsub/64429
// This uses TwitchLib IRC client to capture USERNOTICE viewermilestone events
// which are not yet available through the EventSub websocket API.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Widgets;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace NoMercyBot.Services.Twitch;

public class WatchStreakRecord
{
    [JsonProperty("streak")]
    public int Streak { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("channel_points_reward")]
    public int ChannelPointsReward { get; set; }
}

public class WatchStreakService : IHostedService
{
    private readonly ILogger<WatchStreakService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TwitchChatService _twitchChatService;
    private readonly TtsService _ttsService;
    private TwitchClient? _ircClient;

    private const string RECORD_TYPE = "WatchStreak";
    private const string BOT_VOICE = "en-US-GuyNeural";

    // Snarky streak celebration messages - bot voice
    private static readonly string[] _streakMessages =
    {
        "{name} has watched {streak} streams in a row! That's either dedication or a cry for help. Either way, we appreciate it.",
        "{streak} stream watch streak for {name}! At this point, {name} is basically furniture in this channel.",
        "Alert! {name} has been here for {streak} consecutive streams. Someone check if they're okay.",
        "{name} just hit a {streak} stream watch streak. That's more commitment than most people put into their jobs.",
        "Congratulations {name}! {streak} streams in a row! Your touch grass counter has been reset to zero.",
        "{name} has achieved a {streak} stream watch streak! Their monitor has permanent burn-in of this channel.",
        "Breaking news! {name} has watched {streak} streams straight. The Big Bird is honored. And slightly concerned.",
        "{streak} consecutive streams for {name}! At this point they should be on the payroll.",
    };

    // Messages when someone shares a streak with a custom message
    private static readonly string[] _streakWithMessageTemplates =
    {
        "{name} hit a {streak} stream streak and had this to say:",
        "{name} reached {streak} consecutive streams and dropped this message:",
        "After {streak} streams in a row, {name} felt compelled to share:",
        "{streak} stream streak! {name} has a statement for the chat:",
    };

    // Messages when someone's streak is lower than their record (they lost it and rebuilt)
    private static readonly string[] _rebuiltStreakMessages =
    {
        "{name} is back with a {streak} stream streak! But we remember when it was {record}. The comeback arc has begun.",
        "Look who's rebuilding! {name} had a {record} streak, lost it, and is crawling back at {streak}. Respect the grind.",
        "{name} used to have a legendary {record} stream streak. Now it's {streak}. We don't talk about what happened in between.",
        "The phoenix rises! {name} had a {record} stream record but is back from the ashes with {streak}. Never forget.",
    };

    // Messages when someone beats their own record
    private static readonly string[] _newRecordMessages =
    {
        "{name} just set a NEW personal record! {streak} consecutive streams! The previous record of {record} has been demolished!",
        "HISTORY HAS BEEN MADE! {name} broke their own record of {record} streams with {streak}! Someone give this person a trophy!",
        "{name} is on a RAMPAGE! {streak} streams straight, smashing their old record of {record}! The Big Bird salutes you!",
        "New high score! {name} went from {record} to {streak} consecutive streams! The dedication is frankly alarming!",
    };

    public WatchStreakService(
        ILogger<WatchStreakService> logger,
        IServiceScopeFactory scopeFactory,
        TwitchChatService twitchChatService,
        TtsService ttsService
    )
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _twitchChatService = twitchChatService;
        _ttsService = ttsService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_twitchChatService.IsReady)
        {
            _logger.LogWarning("TwitchChatService not ready, WatchStreakService will not start");
            return Task.CompletedTask;
        }

        _ = Task.Run(
            async () =>
            {
                // Wait briefly for auth to be fully ready
                await Task.Delay(5000, cancellationToken);
                await ConnectIrcClient(cancellationToken);
            },
            cancellationToken
        );

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_ircClient is { IsConnected: true })
        {
            await _ircClient.DisconnectAsync();
        }
    }

    private async Task ConnectIrcClient(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            BotAccount? botAccount = await dbContext.BotAccounts.FirstOrDefaultAsync(
                cancellationToken
            );
            Service? twitchService = await dbContext.Services.FirstOrDefaultAsync(
                s => s.Name == "Twitch",
                cancellationToken
            );

            if (botAccount == null || twitchService == null)
            {
                _logger.LogWarning("Missing bot account or Twitch service for IRC client");
                return;
            }

            string channel = twitchService.UserName;
            string botUsername = botAccount.Username;
            string botToken = botAccount.AccessToken;

            ClientOptions clientOptions = new();
            WebSocketClient wsClient = new(clientOptions);
            _ircClient = new TwitchClient(wsClient);

            ConnectionCredentials credentials = new(botUsername, botToken);
            _ircClient.Initialize(credentials, channel);

            _ircClient.OnSendReceiveData += OnSendReceiveData;
            _ircClient.OnConnected += (_, _) =>
            {
                _logger.LogInformation(
                    "WatchStreak IRC client connected to #{Channel} (temporary until EventSub support)",
                    channel
                );
                return Task.CompletedTask;
            };
            _ircClient.OnDisconnected += async (_, _) =>
            {
                _logger.LogWarning("WatchStreak IRC client disconnected, reconnecting in 5s...");
                await Task.Delay(5000);
                try
                {
                    await _ircClient.ConnectAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError("WatchStreak IRC reconnect failed: {Error}", ex.Message);
                }
            };

            await _ircClient.ConnectAsync();
            _logger.LogInformation("WatchStreak IRC client starting for #{Channel}", channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WatchStreak IRC client");
        }
    }

    private async Task OnSendReceiveData(object? sender, OnSendReceiveDataArgs e)
    {
        // Only process incoming data that contains viewermilestone
        if (e.Direction != SendReceiveDirection.Received)
            return;
        if (!e.Data.Contains("msg-id=viewermilestone"))
            return;

        try
        {
            string rawIrc = e.Data;

            string? category = GetTagValue(rawIrc, "msg-param-category");
            if (category != "watch-streak")
                return;

            string? streakValueStr = GetTagValue(rawIrc, "msg-param-value");
            string? rewardStr = GetTagValue(rawIrc, "msg-param-copoReward");
            string? userId = GetTagValue(rawIrc, "user-id");
            string? displayName = GetTagValue(rawIrc, "display-name");
            string? channel = ExtractChannel(rawIrc);
            string? customMessage = ExtractTrailingMessage(rawIrc);

            if (userId == null || displayName == null || channel == null)
                return;

            if (!int.TryParse(streakValueStr, out int streak))
                return;

            int.TryParse(rewardStr, out int channelPointsReward);

            _logger.LogInformation(
                "Watch streak: {User} has {Streak} consecutive streams (reward: {Points}cp, message: {Message})",
                displayName,
                streak,
                channelPointsReward,
                customMessage ?? "(none)"
            );

            await HandleWatchStreak(
                userId,
                displayName,
                channel,
                streak,
                channelPointsReward,
                customMessage
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling watch streak event");
        }
    }

    private async Task HandleWatchStreak(
        string userId,
        string displayName,
        string channel,
        int streak,
        int channelPointsReward,
        string? customMessage
    )
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get their highest recorded streak
        List<Record> existingRecords = await dbContext
            .Records.AsNoTracking()
            .Where(r => r.UserId == userId && r.RecordType == RECORD_TYPE)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        int highestStreak = 0;
        foreach (Record record in existingRecords)
        {
            WatchStreakRecord? data = record.Data.FromJson<WatchStreakRecord>();
            if (data != null && data.Streak > highestStreak)
                highestStreak = data.Streak;
        }

        // Store this streak
        WatchStreakRecord newRecord = new()
        {
            Streak = streak,
            Message = customMessage,
            ChannelPointsReward = channelPointsReward,
        };

        dbContext.Records.Add(
            new Record
            {
                UserId = userId,
                RecordType = RECORD_TYPE,
                Data = newRecord.ToJson(),
            }
        );
        await dbContext.SaveChangesAsync();

        // Pick the right response template
        string template;
        if (streak > highestStreak && highestStreak > 0)
        {
            // New personal record!
            template = _newRecordMessages[Random.Shared.Next(_newRecordMessages.Length)];
        }
        else if (highestStreak > streak)
        {
            // They had a higher streak before — they lost it and are rebuilding
            template = _rebuiltStreakMessages[Random.Shared.Next(_rebuiltStreakMessages.Length)];
        }
        else if (!string.IsNullOrWhiteSpace(customMessage))
        {
            // Has a custom message
            template = _streakWithMessageTemplates[
                Random.Shared.Next(_streakWithMessageTemplates.Length)
            ];
        }
        else
        {
            // Standard streak celebration
            template = _streakMessages[Random.Shared.Next(_streakMessages.Length)];
        }

        string text = template
            .Replace("{name}", displayName)
            .Replace("{streak}", streak.ToString())
            .Replace("{record}", highestStreak.ToString());

        // If they had a custom message, append it
        if (
            !string.IsNullOrWhiteSpace(customMessage)
            && !template.Contains("had this to say")
            && !template.Contains("dropped this message")
            && !template.Contains("felt compelled")
            && !template.Contains("has a statement")
        )
        {
            text += $" They also said: \"{customMessage}\"";
        }

        // Send chat message
        await _twitchChatService.SendMessageAsBot(channel, text);

        // Dual-voice TTS: bot announces, then reads their custom message if present
        string processedText = await _ttsService.ApplyUsernamePronunciationsAsync(text);

        if (!string.IsNullOrWhiteSpace(customMessage))
        {
            string processedMessage = await _ttsService.ApplyUsernamePronunciationsAsync(
                customMessage
            );
            string? userVoice =
                await _ttsService.GetSpeakerIdForUserAsync(userId, CancellationToken.None)
                ?? "en-US-EmmaMultilingualNeural";

            string botPart = processedText.Replace($"\"{customMessage}\"", "");

            var segments = new List<(string text, string voiceId)>
            {
                TtsService.Segment(BOT_VOICE, botPart),
                TtsService.Segment(userVoice, processedMessage),
            };

            (string? audioBase64, int durationMs) = await _ttsService.SynthesizeMultiVoiceSsmlAsync(
                segments,
                CancellationToken.None
            );

            if (audioBase64 != null)
            {
                IWidgetEventService widgetEventService =
                    scope.ServiceProvider.GetRequiredService<IWidgetEventService>();
                await widgetEventService.PublishEventAsync(
                    "channel.chat.message.tts",
                    new
                    {
                        text,
                        user = new { id = userId },
                        audioBase64,
                        provider = "Edge",
                        cost = 0m,
                        characterCount = text.Length,
                        cached = false,
                    }
                );
            }
        }
        else
        {
            // Single voice - just bot announcing
            await _ttsService.SendCachedTts(processedText, userId, CancellationToken.None);
        }
    }

    private static string? GetTagValue(string rawIrc, string tagName)
    {
        // Parse IRC tags: @key=value;key2=value2 :rest
        if (!rawIrc.StartsWith('@'))
            return null;

        int spaceIndex = rawIrc.IndexOf(' ');
        if (spaceIndex < 0)
            return null;

        string tagsSection = rawIrc[1..spaceIndex];
        foreach (string tag in tagsSection.Split(';'))
        {
            int eqIndex = tag.IndexOf('=');
            if (eqIndex < 0)
                continue;

            string key = tag[..eqIndex];
            if (key == tagName)
            {
                string value = tag[(eqIndex + 1)..];
                // Unescape IRC tag values (\s = space, \\ = backslash)
                return value.Replace("\\s", " ").Replace("\\\\", "\\");
            }
        }

        return null;
    }

    private static string? ExtractChannel(string rawIrc)
    {
        // Format: ... USERNOTICE #channel ...
        int noticeIndex = rawIrc.IndexOf("USERNOTICE #", StringComparison.Ordinal);
        if (noticeIndex < 0)
            return null;

        string afterNotice = rawIrc[(noticeIndex + 12)..];
        int spaceOrEnd = afterNotice.IndexOfAny([' ', '\r', '\n']);
        return spaceOrEnd >= 0 ? afterNotice[..spaceOrEnd] : afterNotice.Trim();
    }

    private static string? ExtractTrailingMessage(string rawIrc)
    {
        // Format: @tags :user USERNOTICE #channel :custom message here
        // The custom message is after "USERNOTICE #channel :"
        int noticeIndex = rawIrc.IndexOf("USERNOTICE #", StringComparison.Ordinal);
        if (noticeIndex < 0)
            return null;

        string afterNotice = rawIrc[noticeIndex..];
        int colonIndex = afterNotice.IndexOf(" :", StringComparison.Ordinal);
        if (colonIndex < 0)
            return null;

        string message = afterNotice[(colonIndex + 2)..].Trim();
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }
}
