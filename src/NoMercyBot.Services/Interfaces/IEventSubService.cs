using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.Interfaces;

public interface IEventSubService
{
    string ProviderName { get; }

    // Verification and handling
    bool VerifySignature(HttpRequest request, string payload);
    Task<IActionResult> HandleEventAsync(HttpRequest request, string payload, string eventType);

    // Subscription management
    Task<List<EventSubscription>> GetAllSubscriptionsAsync();
    Task<EventSubscription?> GetSubscriptionAsync(string id);
    Task<EventSubscription> CreateSubscriptionAsync(string eventType, bool enabled = true);
    Task UpdateSubscriptionAsync(string id, bool enabled);
    Task DeleteSubscriptionAsync(string id);
    Task<bool> DeleteAllSubscriptionsAsync();

    // Event types
    IEnumerable<string> GetAvailableEventTypes();
    Task UpdateAllSubscriptionsAsync(EventSubscription[] subscriptions);
}
