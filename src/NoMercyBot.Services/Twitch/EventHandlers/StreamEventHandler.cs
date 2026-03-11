using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch.Models;
using TwitchLib.EventSub.Core.EventArgs.Stream;
using TwitchLib.EventSub.Websockets;
using Stream = NoMercyBot.Database.Models.Stream;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class StreamEventHandler : TwitchEventHandlerBase
{
    private readonly CancellationToken _cancellationToken;
    private readonly LuckyFeatherTimerService _luckyFeatherTimerService;
    private readonly ShoutoutQueueService _shoutoutQueueService;
    private Stream? _currentStream;

    public StreamEventHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<StreamEventHandler> logger,
        TwitchApiService twitchApiService,
        LuckyFeatherTimerService luckyFeatherTimerService,
        ShoutoutQueueService shoutoutQueueService,
        CancellationToken cancellationToken = default)
        : base(dbContextFactory, logger, twitchApiService)
    {
        _cancellationToken = cancellationToken;
        _luckyFeatherTimerService = luckyFeatherTimerService;
        _shoutoutQueueService = shoutoutQueueService;
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

    private async Task OnStreamOnline(object? sender, StreamOnlineArgs args)
    {
        Logger.LogInformation("Stream online");

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "stream.online",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );

        // Notify Lucky Feather timer to start
        await _luckyFeatherTimerService.OnStreamOnlineAsync(args.Payload.Event.BroadcasterUserId);

        // Reset shoutout queue session for new stream
        _shoutoutQueueService.OnStreamOnline(args.Payload.Event.BroadcasterUserId);

        try
        {
            await using AppDbContext db = await DbContextFactory.CreateDbContextAsync(_cancellationToken);

            ChannelInfo? channelInfo = await db.ChannelInfo
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == args.Payload.Event.BroadcasterUserId, _cancellationToken);

            if (channelInfo != null)
            {
                Stream stream = new()
                {
                    Id = args.Payload.Event.Id,
                    ChannelId = args.Payload.Event.BroadcasterUserId,
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

                await db.Streams.Upsert(stream)
                    .On(p => p.Id)
                    .WhenMatched((existing, entity) => new()
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
                    args.Payload.Event.BroadcasterUserLogin, stream.Id);

                await db.ChannelInfo
                    .Where(c => c.Id == channelInfo.Id)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(c => c.IsLive, true)
                        .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

                Logger.LogInformation("Updated stream status to online for {Channel}",
                    args.Payload.Event.BroadcasterUserLogin);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to handle stream online event for {Channel}: {Message}",
                args.Payload.Event.BroadcasterUserLogin, ex.Message);
        }
    }

    private async Task OnStreamOffline(object? sender, StreamOfflineArgs args)
    {
        Logger.LogInformation("Stream offline");

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "stream.offline",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );

        // Notify Lucky Feather timer to stop
        await _luckyFeatherTimerService.OnStreamOfflineAsync(args.Payload.Event.BroadcasterUserId);

        // Clear shoutout queue session
        _shoutoutQueueService.OnStreamOffline(args.Payload.Event.BroadcasterUserId);

        _currentStream = null;

        await using AppDbContext db = await DbContextFactory.CreateDbContextAsync(_cancellationToken);

        await db.ChannelInfo
            .Where(c => c.Id == args.Payload.Event.BroadcasterUserId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.IsLive, false)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

        await db.Streams
            .OrderByDescending(s => s.CreatedAt)
            .Where(s => s.ChannelId == args.Payload.Event.BroadcasterUserId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);
    }
}
