using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.Widgets;

public class WidgetEventService : IWidgetEventService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<WidgetHub> _hubContext;
    private readonly ILogger<WidgetEventService> _logger;

    public WidgetEventService(
        IServiceScopeFactory serviceScopeFactory,
        IHubContext<WidgetHub> hubContext,
        ILogger<WidgetEventService> logger
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SubscribeWidgetToEventsAsync(Ulid widgetId, List<string> events)
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == widgetId);
        if (widget == null)
            return;

        List<string> currentSubscriptions = widget.EventSubscriptions;
        List<string> newSubscriptions = currentSubscriptions.Union(events).ToList();

        widget.EventSubscriptions = newSubscriptions;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Widget {WidgetId} subscribed to events: {Events}",
            widgetId,
            string.Join(", ", events)
        );
    }

    public async Task UnsubscribeWidgetFromEventsAsync(Ulid widgetId, List<string> events)
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == widgetId);
        if (widget == null)
            return;

        List<string> currentSubscriptions = widget.EventSubscriptions;
        List<string> newSubscriptions = currentSubscriptions.Except(events).ToList();

        widget.EventSubscriptions = newSubscriptions;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Widget {WidgetId} unsubscribed from events: {Events}",
            widgetId,
            string.Join(", ", events)
        );
    }

    public async Task PublishEventAsync(string eventType, object eventData)
    {
        // Get all widgets subscribed to this event type
        List<Ulid> subscribedWidgets = await _dbContext
            .Widgets.Where(w => w.IsEnabled && w.EventSubscriptionsJson.Contains(eventType))
            .Select(w => w.Id)
            .ToListAsync();

        if (subscribedWidgets.Count == 0)
        {
            _logger.LogDebug("No widgets subscribed to event: {EventType}", eventType);
            return;
        }

        var eventPayload = new
        {
            EventType = eventType,
            Data = eventData,
            Timestamp = DateTimeOffset.UtcNow,
        };

        // Send to all subscribed widgets
        foreach (Ulid widgetId in subscribedWidgets)
            await _hubContext
                .Clients.Group($"widget-{widgetId}")
                .SendAsync("WidgetEvent", eventPayload);

        _logger.LogDebug(
            "Published event {EventType} to {Count} widgets",
            eventType,
            subscribedWidgets.Count
        );
    }

    public async Task PublishEventToWidgetAsync(Ulid widgetId, string eventType, object eventData)
    {
        var eventPayload = new
        {
            EventType = eventType,
            Data = eventData,
            Timestamp = DateTimeOffset.UtcNow,
        };

        await _hubContext
            .Clients.Group($"widget-{widgetId}")
            .SendAsync("WidgetEvent", eventPayload);

        _logger.LogDebug("Published event {EventType} to widget {WidgetId}", eventType, widgetId);
    }

    public async Task NotifyWidgetReloadAsync(Ulid widgetId)
    {
        await _hubContext.Clients.Group($"widget-{widgetId}").SendAsync("WidgetReload");

        _logger.LogInformation("Sent reload notification to widget {WidgetId}", widgetId);
    }

    public async Task<List<string>> GetWidgetSubscriptionsAsync(Ulid widgetId)
    {
        Widget? widget = await _dbContext.Widgets.FirstOrDefaultAsync(w => w.Id == widgetId);
        return widget?.EventSubscriptions ?? [];
    }

    public async Task<bool> HasWidgetSubscriptionsAsync(string eventType)
    {
        List<Ulid> subscribedWidgets = await _dbContext
            .Widgets.Where(w => w.IsEnabled && w.EventSubscriptionsJson.Contains(eventType))
            .Select(w => w.Id)
            .ToListAsync();

        if (subscribedWidgets.Count != 0)
            return true;

        _logger.LogDebug("No widgets subscribed to event: {EventType}", eventType);
        return false;
    }
}
