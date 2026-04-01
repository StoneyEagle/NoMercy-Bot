using Microsoft.Extensions.Logging;

namespace NoMercyBot.Services.Widgets;

public interface IWidgetConnectionHandlerRegistry
{
    /// <summary>
    /// Called when a widget joins. Notifies all relevant handlers based on the widget's subscriptions.
    /// </summary>
    Task OnWidgetConnectedAsync(
        Ulid widgetId,
        List<string> subscribedEvents,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Called when a widget disconnects. Notifies all relevant handlers.
    /// </summary>
    Task OnWidgetDisconnectedAsync(
        Ulid widgetId,
        List<string> subscribedEvents,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Register a widget script handler at runtime (after scripts are loaded)
    /// </summary>
    void RegisterScriptHandler(IWidgetConnectionHandler handler);
}

public class WidgetConnectionHandlerRegistry : IWidgetConnectionHandlerRegistry
{
    private readonly List<IWidgetConnectionHandler> _handlers;
    private readonly ILogger<WidgetConnectionHandlerRegistry> _logger;

    public WidgetConnectionHandlerRegistry(
        IEnumerable<IWidgetConnectionHandler> handlers,
        ILogger<WidgetConnectionHandlerRegistry> logger
    )
    {
        _handlers = handlers.ToList();
        _logger = logger;
    }

    public void RegisterScriptHandler(IWidgetConnectionHandler handler)
    {
        _handlers.Add(handler);
        _logger.LogDebug(
            "Registered widget script handler with events: {EventTypes}",
            string.Join(", ", handler.EventTypes)
        );
    }

    public async Task OnWidgetConnectedAsync(
        Ulid widgetId,
        List<string> subscribedEvents,
        CancellationToken cancellationToken = default
    )
    {
        if (subscribedEvents == null || subscribedEvents.Count == 0)
        {
            return;
        }

        HashSet<string> eventSet = new(subscribedEvents, StringComparer.OrdinalIgnoreCase);

        foreach (IWidgetConnectionHandler handler in _handlers)
        {
            // Check if this handler cares about any of the widget's subscribed events
            bool isRelevant = handler.EventTypes.Any(e => eventSet.Contains(e));
            if (!isRelevant)
            {
                continue;
            }

            try
            {
                _logger.LogDebug(
                    "Calling OnConnectedAsync for handler {HandlerType} on widget {WidgetId}",
                    handler.GetType().Name,
                    widgetId
                );
                await handler.OnConnectedAsync(widgetId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Handler {HandlerType} failed on widget connect for {WidgetId}",
                    handler.GetType().Name,
                    widgetId
                );
            }
        }
    }

    public async Task OnWidgetDisconnectedAsync(
        Ulid widgetId,
        List<string> subscribedEvents,
        CancellationToken cancellationToken = default
    )
    {
        if (subscribedEvents == null || subscribedEvents.Count == 0)
        {
            return;
        }

        HashSet<string> eventSet = new(subscribedEvents, StringComparer.OrdinalIgnoreCase);

        foreach (IWidgetConnectionHandler handler in _handlers)
        {
            bool isRelevant = handler.EventTypes.Any(e => eventSet.Contains(e));
            if (!isRelevant)
            {
                continue;
            }

            try
            {
                _logger.LogDebug(
                    "Calling OnDisconnectedAsync for handler {HandlerType} on widget {WidgetId}",
                    handler.GetType().Name,
                    widgetId
                );
                await handler.OnDisconnectedAsync(widgetId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Handler {HandlerType} failed on widget disconnect for {WidgetId}",
                    handler.GetType().Name,
                    widgetId
                );
            }
        }
    }
}
