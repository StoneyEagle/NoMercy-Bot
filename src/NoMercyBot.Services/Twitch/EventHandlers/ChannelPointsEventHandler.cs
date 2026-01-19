using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Twitch.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class ChannelPointsEventHandler : TwitchEventHandlerBase
{
    private readonly TwitchRewardService _twitchRewardService;
    private readonly TwitchRewardChangeService _twitchRewardChangeService;

    public ChannelPointsEventHandler(
        AppDbContext dbContext,
        ILogger<ChannelPointsEventHandler> logger,
        TwitchApiService twitchApiService,
        TwitchRewardService twitchRewardService,
        TwitchRewardChangeService twitchRewardChangeService)
        : base(dbContext, logger, twitchApiService)
    {
        _twitchRewardService = twitchRewardService;
        _twitchRewardChangeService = twitchRewardChangeService;
    }

    public override async Task RegisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelPointsCustomRewardAdd += OnChannelPointsCustomRewardAdd;
        eventSubWebsocketClient.ChannelPointsCustomRewardUpdate += OnChannelPointsCustomRewardUpdate;
        eventSubWebsocketClient.ChannelPointsCustomRewardRemove += OnChannelPointsCustomRewardRemove;
        eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemptionAdd;
        eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionUpdate += OnChannelPointsCustomRewardRedemptionUpdate;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelPointsCustomRewardAdd -= OnChannelPointsCustomRewardAdd;
        eventSubWebsocketClient.ChannelPointsCustomRewardUpdate -= OnChannelPointsCustomRewardUpdate;
        eventSubWebsocketClient.ChannelPointsCustomRewardRemove -= OnChannelPointsCustomRewardRemove;
        eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemptionAdd;
        eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionUpdate -= OnChannelPointsCustomRewardRedemptionUpdate;
        await Task.CompletedTask;
    }

    private async Task OnChannelPointsCustomRewardAdd(object? sender, ChannelPointsCustomRewardArgs args)
    {
        Logger.LogInformation("Custom reward added: {Title}", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.points.custom.reward.add",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPointsCustomRewardUpdate(object? sender, ChannelPointsCustomRewardArgs args)
    {
        Logger.LogInformation("Custom reward updated: {Title}", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.points.custom.reward.update",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );

        // Execute reward change handlers
        await _twitchRewardChangeService.ExecuteRewardChangedAsync(args);
    }

    private async Task OnChannelPointsCustomRewardRemove(object? sender, ChannelPointsCustomRewardArgs args)
    {
        Logger.LogInformation("Custom reward removed: {Title}", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.points.custom.reward.remove",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object? sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        Logger.LogInformation("Reward redeemed: {User} redeemed {Title}",
            args.Payload.Event.UserLogin,
            args.Payload.Event.Reward.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.points.custom.reward.redemption.add",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );

        await _twitchRewardService.ExecuteReward(args);
    }

    private async Task OnChannelPointsCustomRewardRedemptionUpdate(object? sender, ChannelPointsCustomRewardRedemptionArgs args)
    {
        Logger.LogInformation("Reward redemption updated: {User}'s redemption of {Title} was {Status}",
            args.Payload.Event.UserLogin,
            args.Payload.Event.Reward.Title,
            args.Payload.Event.Status);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.points.custom.reward.redemption.update",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );
    }
}
