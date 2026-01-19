using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Twitch.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class PollEventHandler : TwitchEventHandlerBase
{
    public PollEventHandler(
        AppDbContext dbContext,
        ILogger<PollEventHandler> logger,
        TwitchApiService twitchApiService)
        : base(dbContext, logger, twitchApiService)
    {
    }

    public override async Task RegisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelPollBegin += OnChannelPollBegin;
        eventSubWebsocketClient.ChannelPollProgress += OnChannelPollProgress;
        eventSubWebsocketClient.ChannelPollEnd += OnChannelPollEnd;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelPollBegin -= OnChannelPollBegin;
        eventSubWebsocketClient.ChannelPollProgress -= OnChannelPollProgress;
        eventSubWebsocketClient.ChannelPollEnd -= OnChannelPollEnd;
        await Task.CompletedTask;
    }

    private async Task OnChannelPollBegin(object? sender, ChannelPollBeginArgs args)
    {
        Logger.LogInformation("Poll started: \"{Title}\"", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.poll.begin",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPollProgress(object? sender, ChannelPollProgressArgs args)
    {
        Logger.LogInformation("Poll progress: \"{Title}\"", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.poll.progress",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPollEnd(object? sender, ChannelPollEndArgs args)
    {
        Logger.LogInformation("Poll ended: \"{Title}\". Status: {Status}",
            args.Payload.Event.Title,
            args.Payload.Event.Status);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.poll.end",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }
}
