namespace NoMercyBot.Services.Widgets;

/// <summary>
/// Interface for handling widget connection and disconnection events.
/// Implement this interface to send initial state when a widget connects.
/// </summary>
public interface IWidgetConnectionHandler
{
    /// <summary>
    /// The event types this handler is interested in.
    /// When a widget connects and subscribes to any of these events,
    /// the OnConnectedAsync method will be called.
    /// </summary>
    IReadOnlyList<string> EventTypes { get; }

    /// <summary>
    /// Called when a widget joins a group and is subscribed to relevant events.
    /// Use this to send initial state to the widget.
    /// </summary>
    /// <param name="widgetId">The ID of the widget that connected</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnConnectedAsync(Ulid widgetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a widget disconnects.
    /// Use this for any cleanup needed when a widget leaves.
    /// </summary>
    /// <param name="widgetId">The ID of the widget that disconnected</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnDisconnectedAsync(Ulid widgetId, CancellationToken cancellationToken = default);
}
