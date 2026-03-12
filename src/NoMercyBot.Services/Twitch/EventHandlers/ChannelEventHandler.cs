using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch.Models;
using NoMercyBot.Services.Widgets;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class ChannelEventHandler : TwitchEventHandlerBase
{
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchApiService _twitchApiService;
    private readonly TtsService _ttsService;
    private readonly IWidgetEventService _widgetEventService;
    private readonly ShoutoutQueueService _shoutoutQueueService;
    private readonly CancellationToken _cancellationToken;

    public ChannelEventHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<ChannelEventHandler> logger,
        TwitchApiService twitchApiService,
        TtsService ttsService,
        TwitchChatService twitchChatService,
        IWidgetEventService widgetEventService,
        ShoutoutQueueService shoutoutQueueService,
        CancellationToken cancellationToken = default)
        : base(dbContextFactory, logger, twitchApiService)
    {
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _widgetEventService = widgetEventService;
        _ttsService = ttsService;
        _shoutoutQueueService = shoutoutQueueService;
        _cancellationToken = cancellationToken;
    }

    public override async Task RegisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelUpdate += OnChannelUpdate;
        eventSubWebsocketClient.ChannelFollow += OnChannelFollow;
        eventSubWebsocketClient.ChannelRaid += OnChannelRaid;
        eventSubWebsocketClient.ChannelBan += OnChannelBan;
        eventSubWebsocketClient.ChannelUnban += OnChannelUnban;
        eventSubWebsocketClient.ChannelModeratorAdd += OnChannelModeratorAdd;
        eventSubWebsocketClient.ChannelModeratorRemove += OnChannelModeratorRemove;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelUpdate -= OnChannelUpdate;
        eventSubWebsocketClient.ChannelFollow -= OnChannelFollow;
        eventSubWebsocketClient.ChannelRaid -= OnChannelRaid;
        eventSubWebsocketClient.ChannelBan -= OnChannelBan;
        eventSubWebsocketClient.ChannelUnban -= OnChannelUnban;
        eventSubWebsocketClient.ChannelModeratorAdd -= OnChannelModeratorAdd;
        eventSubWebsocketClient.ChannelModeratorRemove -= OnChannelModeratorRemove;
        await Task.CompletedTask;
    }

    private async Task OnChannelUpdate(object? sender, ChannelUpdateArgs args)
    {
        Logger.LogInformation("Channel updated");

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.update",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );

        await using AppDbContext db = await DbContextFactory.CreateDbContextAsync(_cancellationToken);
        await db.ChannelInfo
            .Where(c => c.Id == args.Payload.Event.BroadcasterUserId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.Title, args.Payload.Event.Title)
                .SetProperty(c => c.Language, args.Payload.Event.Language)
                .SetProperty(c => c.GameId, args.Payload.Event.CategoryId)
                .SetProperty(c => c.GameName, args.Payload.Event.CategoryName)
                .SetProperty(c => c.ContentLabels, args.Payload.Event.ContentClassificationLabels.ToList())
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);
    }

    private async Task OnChannelFollow(object? sender, ChannelFollowArgs args)
    {
        Logger.LogInformation("Follow: {User}", args.Payload.Event.UserName);

        try
        {
            await SaveChannelEvent(
                args.Metadata.GetMessageId(),
                "channel.follow",
                args.Payload.Event,
                args.Payload.Event.BroadcasterUserId,
                args.Payload.Event.UserId
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save follow event for {User}, continuing with chat message and TTS",
                args.Payload.Event.UserName);
        }

        try
        {
            await _widgetEventService.PublishEventAsync("channel.follow", new Dictionary<string, string?>
            {
                { "user", args.Payload.Event.UserName }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to publish follow widget event for {User}", args.Payload.Event.UserName);
        }

        try
        {
            string message = $"Thanks for following, @{args.Payload.Event.UserName}! Welcome to the channel!";
            await _twitchChatService.SendMessageAsBot(
                args.Payload.Event.BroadcasterUserLogin, message);

            bool widgetSubscriptions = await _widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
            if (widgetSubscriptions)
            {
                await _ttsService.SendCachedTts(message, args.Payload.Event.BroadcasterUserId, _cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send follow chat message or TTS for {User}", args.Payload.Event.UserName);
        }
    }

    private async Task OnChannelRaid(object? sender, ChannelRaidArgs args)
    {
        Logger.LogInformation("Raid: {FromChannel} raided {ToChannel} with {Viewers} viewers",
            args.Payload.Event.FromBroadcasterUserLogin,
            args.Payload.Event.ToBroadcasterUserLogin,
            args.Payload.Event.Viewers);
        
        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.raid",
            args.Payload.Event,
            args.Payload.Event.ToBroadcasterUserId,
            args.Payload.Event.FromBroadcasterUserId
        );

        await _widgetEventService.PublishEventAsync("channel.raid", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.FromBroadcasterUserName },
            { "viewers", args.Payload.Event.Viewers.ToString() }
        });
        
        if(args.Payload.Event.FromBroadcasterUserId == TwitchConfig.Service().UserId)
        {
            Logger.LogInformation("Raided out to {Channel}", args.Payload.Event.ToBroadcasterUserLogin);
            
            // TODO: Stop OBS broadcasting to Twitch.
            
            await _twitchApiService.SendAnnouncement(
                args.Payload.Event.FromBroadcasterUserId,
                args.Payload.Event.FromBroadcasterUserId,
                $"We have raided out to https://twitch.tv/{args.Payload.Event.ToBroadcasterUserName}, See you there!");
            
            return;
        }

        // Welcome raiders in chat
        string raidMessage = $"{args.Payload.Event.FromBroadcasterUserName} just raided with {args.Payload.Event.Viewers} viewers! Welcome raiders!";
        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.ToBroadcasterUserLogin,
            raidMessage);

        // Queue shoutout for the raider (prioritized ahead of auto-shoutouts)
        _shoutoutQueueService.EnqueueShoutout(
            args.Payload.Event.ToBroadcasterUserId,
            args.Payload.Event.FromBroadcasterUserId,
            args.Payload.Event.ToBroadcasterUserLogin,
            isManual: true,
            isRaid: true);

        bool widgetSubscriptions = await _widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
        if (widgetSubscriptions)
        {
            await _ttsService.SendCachedTts(raidMessage, args.Payload.Event.ToBroadcasterUserId, _cancellationToken);
        }
    }

    private async Task OnChannelBan(object? sender, ChannelBanArgs args)
    {
        Logger.LogInformation("Ban: {User} was banned by {Moderator}. Reason: {Reason}",
            args.Payload.Event.UserLogin,
            args.Payload.Event.ModeratorUserLogin,
            args.Payload.Event.Reason);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.ban",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );

        await _widgetEventService.PublishEventAsync("channel.ban", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.UserName },
            { "moderator", args.Payload.Event.ModeratorUserName },
            { "reason", args.Payload.Event.Reason }
        });

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            $"@{args.Payload.Event.UserName} has been banned from the channel. Reason: {args.Payload.Event.Reason}");
    }

    private async Task OnChannelUnban(object? sender, ChannelUnbanArgs args)
    {
        Logger.LogInformation("Unban: {User} was unbanned by {Moderator}",
            args.Payload.Event.UserLogin,
            args.Payload.Event.ModeratorUserLogin);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.unban",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );

        await _widgetEventService.PublishEventAsync("channel.unban", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.UserName },
            { "moderator", args.Payload.Event.ModeratorUserName }
        });

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            $"@{args.Payload.Event.UserName} has been unbanned from the channel.");
    }

    private async Task OnChannelModeratorAdd(object? sender, ChannelModeratorArgs args)
    {
        Logger.LogInformation("Mod add: {User} was modded", args.Payload.Event.UserLogin);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.moderator.add",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );

        await _widgetEventService.PublishEventAsync("channel.moderator.add", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.UserName },
            { "broadcaster", args.Payload.Event.BroadcasterUserName }
        });

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            $"@{args.Payload.Event.UserName} has been added as a moderator in the channel.");
    }

    private async Task OnChannelModeratorRemove(object? sender, ChannelModeratorArgs args)
    {
        Logger.LogInformation("Mod remove: {User} was unmodded", args.Payload.Event.UserLogin);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.moderator.remove",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );

        await _widgetEventService.PublishEventAsync("channel.moderator.remove", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.UserName },
            { "broadcaster", args.Payload.Event.BroadcasterUserName }
        });

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            $"@{args.Payload.Event.UserName} has been removed as a moderator in the channel.");
    }
}
