namespace NoMercyBot.Services.Widgets;

public interface IWidgetEventService
{
    Task SubscribeWidgetToEventsAsync(Ulid widgetId, List<string> events);
    Task UnsubscribeWidgetFromEventsAsync(Ulid widgetId, List<string> events);
    Task PublishEventAsync(string eventType, object eventData);
    Task PublishEventToWidgetAsync(Ulid widgetId, string eventType, object eventData);
    Task NotifyWidgetReloadAsync(Ulid widgetId);
    Task<List<string>> GetWidgetSubscriptionsAsync(Ulid widgetId);
    Task<bool> HasWidgetSubscriptionsAsync(string widgetType);
}
