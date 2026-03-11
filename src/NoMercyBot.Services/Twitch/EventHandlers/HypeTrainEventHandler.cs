using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Twitch.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class HypeTrainEventHandler : TwitchEventHandlerBase
{
    public HypeTrainEventHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<HypeTrainEventHandler> logger,
        TwitchApiService twitchApiService)
        : base(dbContextFactory, logger, twitchApiService)
    {
    }

    public override async Task RegisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelHypeTrainBeginV2 += OnHypeTrainBegin;
        eventSubWebsocketClient.ChannelHypeTrainProgressV2 += OnHypeTrainProgress;
        eventSubWebsocketClient.ChannelHypeTrainEndV2 += OnHypeTrainEnd;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelHypeTrainBeginV2 -= OnHypeTrainBegin;
        eventSubWebsocketClient.ChannelHypeTrainProgressV2 -= OnHypeTrainProgress;
        eventSubWebsocketClient.ChannelHypeTrainEndV2 -= OnHypeTrainEnd;
        await Task.CompletedTask;
    }

    private async Task OnHypeTrainBegin(object? sender, ChannelHypeTrainBeginV2Args args)
    {
        Logger.LogInformation("Hype Train started");

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.hype.train.begin",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnHypeTrainProgress(object? sender, ChannelHypeTrainProgressV2Args args)
    {
        Logger.LogInformation("Hype Train progress: Level {Level}, {Points}/{Goal} points",
            args.Payload.Event.Level,
            args.Payload.Event.Progress,
            args.Payload.Event.Goal);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.hype.train.progress",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnHypeTrainEnd(object? sender, ChannelHypeTrainEndV2Args args)
    {
        Logger.LogInformation("Hype Train ended. Reached Level {Level}",
            args.Payload.Event.Level);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.hype.train.end",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }
}
