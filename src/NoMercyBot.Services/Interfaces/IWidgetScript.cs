using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Services.Interfaces;

public interface IWidgetScript
{
    /// <summary>
    /// The event types this handler is interested in.
    /// When a widget connects and subscribes to any of these events,
    /// the OnConnected method will be called.
    /// </summary>
    IReadOnlyList<string> EventTypes { get; }

    /// <summary>
    /// Called once when the script is loaded
    /// </summary>
    Task Init(WidgetScriptContext context);

    /// <summary>
    /// Called when a widget joins a group and is subscribed to relevant events.
    /// Use this to send initial state to the widget.
    /// </summary>
    Task OnConnected(WidgetScriptContext context, Ulid widgetId);

    /// <summary>
    /// Called when a widget disconnects.
    /// Use this for any cleanup needed when a widget leaves.
    /// </summary>
    Task OnDisconnected(WidgetScriptContext context, Ulid widgetId);
}
