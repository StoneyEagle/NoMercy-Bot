using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Twitch.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class OtherEventHandler : TwitchEventHandlerBase
{
    private readonly TwitchChatService _twitchChatService;

    public OtherEventHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<OtherEventHandler> logger,
        TwitchApiService twitchApiService,
        TwitchChatService twitchChatService
    )
        : base(dbContextFactory, logger, twitchApiService)
    {
        _twitchChatService = twitchChatService;
    }

    public override async Task RegisterEventHandlersAsync(
        EventSubWebsocketClient eventSubWebsocketClient
    )
    {
        eventSubWebsocketClient.ChannelShieldModeBegin += OnChannelShieldModeBegin;
        eventSubWebsocketClient.ChannelShieldModeEnd += OnChannelShieldModeEnd;
        eventSubWebsocketClient.ChannelShoutoutCreate += OnShoutoutCreate;
        eventSubWebsocketClient.ChannelShoutoutReceive += OnShoutoutReceived;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(
        EventSubWebsocketClient eventSubWebsocketClient
    )
    {
        eventSubWebsocketClient.ChannelShieldModeBegin -= OnChannelShieldModeBegin;
        eventSubWebsocketClient.ChannelShieldModeEnd -= OnChannelShieldModeEnd;
        eventSubWebsocketClient.ChannelShoutoutCreate -= OnShoutoutCreate;
        eventSubWebsocketClient.ChannelShoutoutReceive -= OnShoutoutReceived;
        await Task.CompletedTask;
    }

    private async Task OnChannelShieldModeBegin(object? sender, ChannelShieldModeBeginArgs args)
    {
        Logger.LogInformation(
            "Shield mode activated by {Moderator}",
            args.Payload.Event.ModeratorUserLogin
        );

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.shield.mode.begin",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.ModeratorUserId
        );
    }

    private async Task OnChannelShieldModeEnd(object? sender, ChannelShieldModeEndArgs args)
    {
        Logger.LogInformation(
            "Shield mode deactivated by {Moderator}",
            args.Payload.Event.ModeratorUserLogin
        );

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.shield.mode.end",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.ModeratorUserId
        );
    }

    private async Task OnShoutoutCreate(object? sender, ChannelShoutoutCreateArgs args)
    {
        Logger.LogInformation("Shouted out {ToChannel}", args.Payload.Event.ToBroadcasterUserLogin);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.shoutout.create",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.ToBroadcasterUserId
        );
    }

    private async Task OnShoutoutReceived(object? sender, ChannelShoutoutReceiveArgs args)
    {
        Logger.LogInformation(
            "Shoutout received from {FromChannel} with {ViewerCount} viewers",
            args.Payload.Event.FromBroadcasterUserLogin,
            args.Payload.Event.ViewerCount
        );

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.shoutout.receive",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.FromBroadcasterUserId
        );

        // _ = await TwitchApiService.GetOrFetchUser(args.Payload.Event.FromBroadcasterUserId);

        // await _twitchChatService.SendOneOffMessage(
        //     args.Payload.Event.FromBroadcasterUserId,
        //     $"Thank you @{args.Payload.Event.FromBroadcasterUserName} for the shoutout, I appreciate it!"
        // );
    }
}
