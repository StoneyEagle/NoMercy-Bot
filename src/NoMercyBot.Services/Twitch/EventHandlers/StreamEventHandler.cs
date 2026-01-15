using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
using Stream = NoMercyBot.Database.Models.Stream;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class StreamEventHandler : TwitchEventHandlerBase
{
    private readonly CancellationToken _cancellationToken;
    private readonly LuckyFeatherTimerService _luckyFeatherTimerService;
    private Stream? _currentStream;

    public StreamEventHandler(
        AppDbContext dbContext,
        ILogger<StreamEventHandler> logger,
        TwitchApiService twitchApiService,
        CancellationToken cancellationToken = default)
        : base(dbContext, logger, twitchApiService)
    {
        _cancellationToken = cancellationToken;
    }

    public Stream? CurrentStream => _currentStream;

    public override async Task RegisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.StreamOnline += OnStreamOnline;
        eventSubWebsocketClient.StreamOffline += OnStreamOffline;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.StreamOnline -= OnStreamOnline;
        eventSubWebsocketClient.StreamOffline -= OnStreamOffline;
        await Task.CompletedTask;
    }

    private async Task OnStreamOnline(object sender, StreamOnlineArgs args)
    {
        Logger.LogInformation("Stream online");

        await SaveChannelEvent(
            args.Notification.Metadata.MessageId,
            "stream.online",
            args.Notification.Payload.Event,
            args.Notification.Payload.Event.BroadcasterUserId
        );

        // Notify Lucky Feather timer to start
        await _luckyFeatherTimerService.OnStreamOnlineAsync(args.Notification.Payload.Event.BroadcasterUserId);

        try
        {
            ChannelInfo? channelInfo = await DbContext.ChannelInfo
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == args.Notification.Payload.Event.BroadcasterUserId, _cancellationToken);

            if (channelInfo != null)
            {
                Stream stream = new()
                {
                    Id = args.Notification.Payload.Event.Id,
                    ChannelId = args.Notification.Payload.Event.BroadcasterUserId,
                    Title = channelInfo.Title,
                    GameId = channelInfo.GameId,
                    GameName = channelInfo.GameName,
                    Language = channelInfo.Language,
                    Delay = channelInfo.Delay,
                    Tags = channelInfo.Tags,
                    ContentLabels = channelInfo.ContentLabels,
                    IsBrandedContent = channelInfo.IsBrandedContent
                };

                _currentStream = stream;

                await DbContext.Streams.Upsert(stream)
                    .On(p => p.Id)
                    .WhenMatched((db, entity) => new()
                    {
                        Title = entity.Title,
                        GameId = entity.GameId,
                        GameName = entity.GameName,
                        Language = entity.Language,
                        Delay = entity.Delay,
                        Tags = entity.Tags,
                        ContentLabels = entity.ContentLabels,
                        IsBrandedContent = entity.IsBrandedContent
                    })
                    .RunAsync();

                Logger.LogInformation("Created new stream entry for {Channel} with ID {StreamId}",
                    args.Notification.Payload.Event.BroadcasterUserLogin, stream.Id);

                await DbContext.ChannelInfo
                    .Where(c => c.Id == channelInfo.Id)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(c => c.IsLive, true)
                        .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

                Logger.LogInformation("Updated stream status to online for {Channel}",
                    args.Notification.Payload.Event.BroadcasterUserLogin);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to handle stream online event for {Channel}: {Message}",
                args.Notification.Payload.Event.BroadcasterUserLogin, ex.Message);
        }
    }

    private async Task OnStreamOffline(object sender, StreamOfflineArgs args)
    {
        Logger.LogInformation("Stream offline");

        await SaveChannelEvent(
            args.Notification.Metadata.MessageId,
            "stream.offline",
            args.Notification.Payload.Event,
            args.Notification.Payload.Event.BroadcasterUserId
        );

        // Notify Lucky Feather timer to stop
        await _luckyFeatherTimerService.OnStreamOfflineAsync(args.Notification.Payload.Event.BroadcasterUserId);

        _currentStream = null;

        await DbContext.ChannelInfo
            .Where(c => c.Id == args.Notification.Payload.Event.BroadcasterUserId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.IsLive, false)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

        await DbContext.Streams
            .OrderByDescending(s => s.CreatedAt)
            .Where(s => s.ChannelId == args.Notification.Payload.Event.BroadcasterUserId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);
    }
}
