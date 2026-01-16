using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Spotify;

namespace NoMercyBot.Services.Widgets;

public class WidgetHub : Hub
{
    private readonly ILogger<WidgetHub> _logger;
    private readonly IWidgetEventService _widgetEventService;
    private readonly IWidgetConnectionHandlerRegistry _connectionHandlerRegistry;
    private readonly SpotifyApiService _spotifyApiService;

    public WidgetHub(ILogger<WidgetHub> logger,
        SpotifyApiService spotifyApiService,
        IWidgetEventService widgetEventService,
        IWidgetConnectionHandlerRegistry connectionHandlerRegistry)
    {
        _logger = logger;
        _widgetEventService = widgetEventService;
        _connectionHandlerRegistry = connectionHandlerRegistry;
        _spotifyApiService = spotifyApiService;
    }

    public async Task JoinWidgetGroup(string widgetId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"widget-{widgetId}");
        _logger.LogDebug("Connection {ConnectionId} joined widget group {WidgetId}", Context.ConnectionId, widgetId);

        // Parse widget ID and get subscriptions
        if (Ulid.TryParse(widgetId, out Ulid parsedWidgetId))
        {
            List<string> subscriptions = await _widgetEventService.GetWidgetSubscriptionsAsync(parsedWidgetId);

            // Notify connection handlers (runs in background to not block the join)
            _ = Task.Run(async () =>
            {
                // Small delay to ensure the widget is fully connected and ready to receive events
                await Task.Delay(500);
                await _connectionHandlerRegistry.OnWidgetConnectedAsync(parsedWidgetId, subscriptions);
            });
        }

        // Existing Spotify state push (after delay)
        _ = Task.Delay(5000).ContinueWith(async _ =>
        {
            await _widgetEventService.PublishEventAsync("spotify.state.changed", _spotifyApiService.SpotifyState);
        });
    }

    public async Task LeaveWidgetGroup(string widgetId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"widget-{widgetId}");
        _logger.LogDebug("Connection {ConnectionId} left widget group {WidgetId}", Context.ConnectionId, widgetId);

        // Notify connection handlers of disconnect
        if (Ulid.TryParse(widgetId, out Ulid parsedWidgetId))
        {
            List<string> subscriptions = await _widgetEventService.GetWidgetSubscriptionsAsync(parsedWidgetId);
            await _connectionHandlerRegistry.OnWidgetDisconnectedAsync(parsedWidgetId, subscriptions);
        }
    }

    public async Task NotifyServerShutdown()
    {
        _logger.LogInformation("Notifying all widget connections of server shutdown");
        await Clients.All.SendAsync("ServerShutdown");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Widget connection established: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Widget connection disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}