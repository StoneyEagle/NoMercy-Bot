using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Twitch.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class PredictionEventHandler : TwitchEventHandlerBase
{
    public PredictionEventHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<PredictionEventHandler> logger,
        TwitchApiService twitchApiService
    )
        : base(dbContextFactory, logger, twitchApiService) { }

    public override async Task RegisterEventHandlersAsync(
        EventSubWebsocketClient eventSubWebsocketClient
    )
    {
        eventSubWebsocketClient.ChannelPredictionBegin += OnChannelPredictionBegin;
        eventSubWebsocketClient.ChannelPredictionProgress += OnChannelPredictionProgress;
        eventSubWebsocketClient.ChannelPredictionLock += OnChannelPredictionLock;
        eventSubWebsocketClient.ChannelPredictionEnd += OnChannelPredictionEnd;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(
        EventSubWebsocketClient eventSubWebsocketClient
    )
    {
        eventSubWebsocketClient.ChannelPredictionBegin -= OnChannelPredictionBegin;
        eventSubWebsocketClient.ChannelPredictionProgress -= OnChannelPredictionProgress;
        eventSubWebsocketClient.ChannelPredictionLock -= OnChannelPredictionLock;
        eventSubWebsocketClient.ChannelPredictionEnd -= OnChannelPredictionEnd;
        await Task.CompletedTask;
    }

    private async Task OnChannelPredictionBegin(object? sender, ChannelPredictionBeginArgs args)
    {
        Logger.LogInformation("Prediction started: \"{Title}\"", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.prediction.begin",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPredictionProgress(
        object? sender,
        ChannelPredictionProgressArgs args
    )
    {
        Logger.LogInformation("Prediction progress: \"{Title}\"", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.prediction.progress",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPredictionLock(object? sender, ChannelPredictionLockArgs args)
    {
        Logger.LogInformation("Prediction locked: \"{Title}\"", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.prediction.lock",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPredictionEnd(object? sender, ChannelPredictionEndArgs args)
    {
        Logger.LogInformation(
            "Prediction ended: \"{Title}\". Status: {Status}",
            args.Payload.Event.Title,
            args.Payload.Event.Status
        );

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.prediction.end",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }
}
