using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Obs;

public class ObsEventSubService : IEventSubService
{
    private readonly ILogger<ObsEventSubService> _logger;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ObsApiService _obsApiService;

    public string ProviderName => "obs";

    internal static readonly Dictionary<string, string> AvailableEventTypes = new()
    {
        { "scene.changed", "When the active scene in OBS is changed" },
        { "stream.started", "When streaming begins in OBS" },
        { "stream.stopped", "When streaming ends in OBS" },
        { "recording.started", "When recording begins in OBS" },
        { "recording.stopped", "When recording ends in OBS" },
        { "source.visibility.changed", "When a source's visibility is toggled in OBS" },
        { "media.started", "When media playback begins in OBS" },
        { "media.ended", "When media playback ends in OBS" },
        { "scene.item.added", "When an item is added to a scene in OBS" },
        { "scene.item.removed", "When an item is removed from a scene in OBS" },
        { "scene.item.visibility.changed", "When an item's visibility is toggled in a scene" },
        { "scene.collection.changed", "When the scene collection is changed in OBS" },
        { "exit.started", "When OBS begins to shut down" },
        { "recording.paused", "When recording is paused in OBS" },
        { "recording.resumed", "When recording is resumed in OBS" },
        { "streaming.status", "When the streaming status changes in OBS" },
        { "virtual.cam.started", "When the virtual camera is started in OBS" },
        { "virtual.cam.stopped", "When the virtual camera is stopped in OBS" },
    };

    public ObsEventSubService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ObsEventSubService> logger,
        ObsApiService obsApiService
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _obsApiService = obsApiService;
    }

    public bool VerifySignature(HttpRequest request, string payload)
    {
        // OBS WebSocket doesn't use signatures for incoming webhooks in the same way
        // as Twitch or Discord. This is typically used for incoming events from services.
        // For a local OBS WebSocket connection, authentication happens when establishing
        // the connection, not on each message.

        // For an external web-hook style integration, you would implement your own
        // authentication mechanism.
        return true;
    }

    public async Task<IActionResult> HandleEventAsync(
        HttpRequest request,
        string payload,
        string eventType
    )
    {
        try
        {
            // Parse the event payload
            JObject? json = payload.FromJson<JObject>();

            if (json == null)
                return new BadRequestObjectResult("Invalid payload");

            // Check if this event type is enabled
            EventSubscription? subscription =
                await _dbContext.EventSubscriptions.FirstOrDefaultAsync(s =>
                    s.Provider == ProviderName && s.EventType == eventType
                );

            if (subscription == null || !subscription.Enabled)
            {
                _logger.LogInformation("Ignoring disabled OBS event: {EventType}", eventType);
                return new OkResult();
            }

            // Process the event based on type
            await ProcessObsEvent(eventType, json);

            return new OkResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling OBS event");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    private Task ProcessObsEvent(string eventType, JObject payload)
    {
        try
        {
            _logger.LogInformation("Processing OBS event: {EventType}", eventType);

            switch (eventType)
            {
                case "scene.changed":
                    string? sceneName = payload["scene_name"]?.ToString();
                    _logger.LogInformation("Scene changed to: {SceneName}", sceneName);
                    break;

                case "stream.started":
                    _logger.LogInformation("Stream started");
                    break;

                case "stream.stopped":
                    _logger.LogInformation("Stream stopped");
                    break;

                default:
                    _logger.LogInformation(
                        "Received OBS event: {EventType} with payload: {Payload}",
                        eventType,
                        payload.ToString()
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OBS event");
        }

        return Task.CompletedTask;
    }

    public async Task<List<EventSubscription>> GetAllSubscriptionsAsync()
    {
        return await _dbContext
            .EventSubscriptions.Where(s => s.Provider == ProviderName)
            .ToListAsync();
    }

    public async Task<EventSubscription?> GetSubscriptionAsync(string id)
    {
        return await _dbContext.EventSubscriptions.FirstOrDefaultAsync(s =>
            s.Provider == ProviderName && s.Id == id
        );
    }

    public async Task<EventSubscription> CreateSubscriptionAsync(
        string eventType,
        bool enabled = true
    )
    {
        // Check if event type is valid
        if (!AvailableEventTypes.ContainsKey(eventType))
            throw new ArgumentException($"Invalid event type: {eventType}");

        // Check if subscription already exists
        EventSubscription? existingSub = await _dbContext.EventSubscriptions.FirstOrDefaultAsync(
            s => s.Provider == ProviderName && s.EventType == eventType
        );

        if (existingSub != null)
        {
            existingSub.Enabled = enabled;
            existingSub.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // If we're enabling an existing subscription and have an active connection,
            // make sure OBS is set up to handle this event
            if (enabled && ObsConfig.Service().Enabled)
                await RegisterObsEventHandler(eventType);

            return existingSub;
        }

        // Create new subscription
        EventSubscription subscription = new(ProviderName, eventType, enabled);

        // If subscription is enabled and OBS is connected, register the event handler
        if (enabled && ObsConfig.Service().Enabled)
            await RegisterObsEventHandler(eventType);

        await _dbContext.EventSubscriptions.AddAsync(subscription);
        await _dbContext.SaveChangesAsync();

        return subscription;
    }

    public Task UpdateAllSubscriptionsAsync(EventSubscription[] subscriptions)
    {
        if (subscriptions == null || subscriptions.Length == 0)
            throw new ArgumentException("No subscriptions provided to update");

        foreach (EventSubscription sub in subscriptions)
        {
            if (sub.Provider != ProviderName)
                throw new ArgumentException($"Invalid provider for subscription: {sub.Id}");

            _dbContext.EventSubscriptions.Update(sub);
        }

        return _dbContext.SaveChangesAsync();
    }

    private Task RegisterObsEventHandler(string eventType)
    {
        try
        {
            // This would typically involve setting up WebSocket event handlers
            // or other mechanisms specific to OBS
            _logger.LogInformation("Registering OBS event handler for: {EventType}", eventType);

            // Implement OBS-specific registration logic here
            // This depends on how your ObsApiService is implemented
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering OBS event handler for {EventType}", eventType);
        }

        return Task.CompletedTask;
    }

    public async Task UpdateSubscriptionAsync(string id, bool enabled)
    {
        EventSubscription? subscription = await _dbContext.EventSubscriptions.FirstOrDefaultAsync(
            s => s.Provider == ProviderName && s.Id == id
        );

        if (subscription == null)
            throw new KeyNotFoundException($"Subscription not found: {id}");

        bool wasEnabled = subscription.Enabled;
        subscription.Enabled = enabled;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // If we're enabling or disabling, we need to update OBS handlers
        if (wasEnabled != enabled && ObsConfig.Service().Enabled)
        {
            if (enabled)
            {
                await RegisterObsEventHandler(subscription.EventType);
            }
            else
            {
                // Logic to unregister the event handler
            }
        }
    }

    public async Task DeleteSubscriptionAsync(string id)
    {
        EventSubscription? subscription = await _dbContext.EventSubscriptions.FirstOrDefaultAsync(
            s => s.Provider == ProviderName && s.Id == id
        );

        if (subscription == null)
            return;

        // If subscription is enabled, unregister the event handler
        if (subscription.Enabled && ObsConfig.Service().Enabled)
        {
            // Logic to unregister the event handler
        }

        _dbContext.EventSubscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAllSubscriptionsAsync()
    {
        try
        {
            List<EventSubscription> subscriptions = await _dbContext
                .EventSubscriptions.Where(s => s.Provider == ProviderName)
                .ToListAsync();

            // If OBS is connected, unregister all event handlers
            if (ObsConfig.Service().Enabled)
            {
                // Logic to unregister all event handlers
            }

            _dbContext.EventSubscriptions.RemoveRange(subscriptions);
            await _dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all OBS subscriptions");
            return false;
        }
    }

    public IEnumerable<string> GetAvailableEventTypes()
    {
        return AvailableEventTypes.Keys;
    }
}
