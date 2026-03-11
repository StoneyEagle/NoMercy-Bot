using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch.EventHandlers;
using NoMercyBot.Services.Twitch.EventHandlers.Interfaces;
using NoMercyBot.Services.Widgets;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using Stream = NoMercyBot.Database.Models.Stream;

namespace NoMercyBot.Services.Twitch;

public class TwitchWebsocketHostedService : IHostedService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly EventSubWebsocketClient _eventSubWebsocketClient;
    private readonly ILogger<TwitchWebsocketHostedService> _logger;
    private CancellationTokenSource _cts = new();
    private readonly TwitchAPI _twitchApi = new();
    private readonly TwitchApiService _twitchApiService;
    private readonly TwitchEventSubService _twitchEventSubService;
    private readonly List<ITwitchEventHandler> _eventHandlers = [];
    private bool _isConnected;

    // Event handlers
    private readonly UserEventHandler _userEventHandler;
    private readonly ChannelEventHandler _channelEventHandler;
    private readonly MonetizationEventHandler _monetizationEventHandler;
    private readonly ChatEventHandler _chatEventHandler;
    private readonly StreamEventHandler _streamEventHandler;
    private readonly ChannelPointsEventHandler _channelPointsEventHandler;
    private readonly PollEventHandler _pollEventHandler;
    private readonly PredictionEventHandler _predictionEventHandler;
    private readonly HypeTrainEventHandler _hypeTrainEventHandler;
    private readonly OtherEventHandler _otherEventHandler;

    public TwitchWebsocketHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<TwitchWebsocketHostedService> logger,
        EventSubWebsocketClient eventSubWebsocketClient,
        TwitchApiService twitchApiService,
        TwitchEventSubService twitchEventSubService,
        TwitchMessageDecorator twitchMessageDecorator,
        TtsService ttsService,
        TwitchCommandService twitchCommandService,
        TwitchRewardService twitchRewardService,
        TwitchChatService twitchChatService,
        IWidgetEventService widgetEventService,
        LuckyFeatherTimerService luckyFeatherTimerService,
        ShoutoutQueueService shoutoutQueueService)
    {
        IServiceScope scope = serviceScopeFactory.CreateScope();
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _eventSubWebsocketClient = eventSubWebsocketClient;
        _twitchApiService = twitchApiService;
        _twitchEventSubService = twitchEventSubService;

        // Initialize event handlers
        _userEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<UserEventHandler>>(), twitchApiService);
        _channelEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<ChannelEventHandler>>(), twitchApiService, ttsService, twitchChatService, widgetEventService, shoutoutQueueService, _cts.Token);
        _monetizationEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<MonetizationEventHandler>>(), twitchApiService, twitchChatService, widgetEventService, ttsService, _cts.Token);
        _chatEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<ChatEventHandler>>(), twitchApiService, twitchChatService, twitchCommandService, twitchMessageDecorator, widgetEventService, ttsService, shoutoutQueueService, _cts.Token);
        _streamEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<StreamEventHandler>>(), twitchApiService, luckyFeatherTimerService, shoutoutQueueService, _cts.Token);
        _channelPointsEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<ChannelPointsEventHandler>>(), twitchApiService, twitchRewardService, scope.ServiceProvider.GetRequiredService<TwitchRewardChangeService>());
        _pollEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<PollEventHandler>>(), twitchApiService);
        _predictionEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<PredictionEventHandler>>(), twitchApiService);
        _hypeTrainEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<HypeTrainEventHandler>>(), twitchApiService);
        _otherEventHandler = new(dbContextFactory, scope.ServiceProvider.GetRequiredService<ILogger<OtherEventHandler>>(), twitchApiService, twitchChatService);

        // Add all handlers to the list
        _eventHandlers.AddRange([
            _userEventHandler,
            _channelEventHandler,
            _monetizationEventHandler,
            _chatEventHandler,
            _streamEventHandler,
            _channelPointsEventHandler,
            _pollEventHandler,
            _predictionEventHandler,
            _hypeTrainEventHandler,
            _otherEventHandler
        ]);

        // Subscribe to the event
        twitchEventSubService.OnEventSubscriptionChanged += HandleEventSubscriptionChange;

        // Initialize current stream reference and pass it to chat handler
        using AppDbContext initDb = _dbContextFactory.CreateDbContext();
        Stream? currentStream = initDb.Streams
            .FirstOrDefault(stream => stream.UpdatedAt == stream.CreatedAt);
        _chatEventHandler.SetCurrentStream(currentStream);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TwitchWebsocketHostedService starting.");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Basic connection events
        _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
        _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
        _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
        _eventSubWebsocketClient.ErrorOccurred += OnError;

        // Register all event handlers
        foreach (ITwitchEventHandler handler in _eventHandlers)
        {
            await handler.RegisterEventHandlersAsync(_eventSubWebsocketClient);
        }

        // Check if Twitch service is properly configured before connecting
        Service twitchService = TwitchConfig.Service();
        if (string.IsNullOrEmpty(twitchService.ClientId) ||
            string.IsNullOrEmpty(twitchService.ClientSecret) ||
            string.IsNullOrEmpty(twitchService.AccessToken))
        {
            _logger.LogWarning("TwitchWebsocketHostedService: Twitch service not fully configured. Waiting for authentication...");

            // Poll for credentials to become available (max 5 minutes)
            int maxAttempts = 60;
            for (int i = 0; i < maxAttempts && !cancellationToken.IsCancellationRequested; i++)
            {
                await Task.Delay(5000, cancellationToken);

                // Reload from database
                await using AppDbContext pollDb = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                Service? refreshedService = await pollDb.Services
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Name == "Twitch", cancellationToken);

                if (refreshedService != null &&
                    !string.IsNullOrEmpty(refreshedService.AccessToken) &&
                    !string.IsNullOrEmpty(refreshedService.ClientId))
                {
                    TwitchConfig._service = refreshedService;
                    _logger.LogInformation("TwitchWebsocketHostedService: Twitch credentials now available. Proceeding with connection.");
                    break;
                }

                if (i > 0 && i % 12 == 0)
                {
                    _logger.LogInformation("TwitchWebsocketHostedService: Still waiting for Twitch authentication... ({Elapsed}s)", (i + 1) * 5);
                }
            }

            // Check again after waiting
            twitchService = TwitchConfig.Service();
            if (string.IsNullOrEmpty(twitchService.AccessToken))
            {
                _logger.LogError("TwitchWebsocketHostedService: Twitch authentication not completed. WebSocket will not connect.");
                return;
            }
        }

        // Set up TwitchAPI credentials
        _twitchApi.Settings.ClientId = twitchService.ClientId;
        _twitchApi.Settings.Secret = twitchService.ClientSecret;
        _twitchApi.Settings.AccessToken = twitchService.AccessToken;

        // Connect to EventSub WebSocket
        await _eventSubWebsocketClient.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TwitchWebsocketHostedService stopping.");

        // Unsubscribe from event changes
        _twitchEventSubService.OnEventSubscriptionChanged -= HandleEventSubscriptionChange;

        // Unregister all event handlers
        foreach (ITwitchEventHandler handler in _eventHandlers)
        {
            await handler.UnregisterEventHandlersAsync(_eventSubWebsocketClient);
        }

        try
        {
            // Cancel the internal CTS first to stop any ongoing operations
            await _cts.CancelAsync();

            // Try to disconnect with a timeout to prevent hanging
            using CancellationTokenSource timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5)); // 5-second timeout for disconnect

            await _eventSubWebsocketClient.DisconnectAsync();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("TwitchWebsocketHostedService shutdown was cancelled or timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TwitchWebsocketHostedService shutdown");
        }

        _logger.LogInformation("TwitchWebsocketHostedService stopped.");
    }

    private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
    {
        _logger.LogInformation("Twitch EventSub WebSocket connected. Session ID: {SessionId}",
            _eventSubWebsocketClient.SessionId);
        _isConnected = true;

        if (!e.IsRequestedReconnect)
        {
            // Get broadcaster ID from configuration
            string? accessToken = TwitchConfig.Service().AccessToken;
            string broadcasterId = TwitchConfig.Service().UserId;

            if (string.IsNullOrEmpty(broadcasterId) || string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Cannot subscribe to events: Missing broadcaster ID or access token");
                return;
            }

            try
            {
                // Get all enabled Twitch event subscriptions from the database
                await using AppDbContext subDb = await _dbContextFactory.CreateDbContextAsync(_cts.Token);
                List<EventSubscription> enabledSubscriptions = await subDb.EventSubscriptions
                    .Where(s => s.Provider == "twitch" && s.Enabled)
                    .ToListAsync(_cts.Token);

                if (enabledSubscriptions.Count == 0)
                {
                    _logger.LogInformation("No enabled Twitch event subscriptions found");
                    return;
                }

                _logger.LogInformation("Subscribing to {Count} Twitch events",
                    enabledSubscriptions.Count);

                // Subscribe to all enabled event subscriptions - each database record creates one subscription
                await Parallel.ForEachAsync(enabledSubscriptions, async (subscription, _) =>
                {
                    try
                    {
                        // Each database record has its own condition configuration
                        // Parse the Conditions field from the database record
                        if (subscription.Condition.Length == 0)
                        {
                            _logger.LogWarning("Subscription {EventType} (ID: {Id}) has no conditions defined", 
                                subscription.EventType, subscription.Id);
                            return;
                        }

                        // Create condition dictionary from the database record's conditions
                        Dictionary<string, string> condition = [];
                        
                        foreach (string conditionParam in subscription.Condition)
                        {
                            switch (conditionParam)
                            {
                                case "broadcaster_user_id":
                                    condition["broadcaster_user_id"] = broadcasterId;
                                    break;
                                
                                case "to_broadcaster_user_id":
                                    condition["to_broadcaster_user_id"] = broadcasterId;
                                    break;
                                
                                case "from_broadcaster_user_id":
                                    condition["from_broadcaster_user_id"] = broadcasterId;
                                    break;

                                case "moderator_user_id":
                                    condition["moderator_user_id"] = broadcasterId;
                                    break;

                                case "client_id":
                                    condition["client_id"] = TwitchConfig.Service().ClientId!;
                                    break;

                                case "user_id":
                                    condition["user_id"] = broadcasterId;
                                    break;

                                case "extension_client_id":
                                    condition["extension_client_id"] = TwitchConfig.Service().ClientId!;
                                    break;

                                default:
                                    _logger.LogWarning("Unknown condition parameter: {ConditionParam} for event type {EventType}",
                                        conditionParam, subscription.EventType);
                                    break;
                            }
                        }

                        // Create one subscription for this database record
                        await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            subscription.EventType,
                            subscription.Version,
                            condition,
                            EventSubTransportMethod.Websocket,
                            _eventSubWebsocketClient.SessionId,
                            accessToken: accessToken);

                        _logger.LogInformation(
                            "Successfully subscribed to {EventType} (version {Version}) with {Conditions}",
                            subscription.EventType, subscription.Version ?? "1", 
                            string.Join(", ", condition.Select(kvp => $"{kvp.Key}={kvp.Value}")));

                        // Update the SessionId in the database
                        subscription.SessionId = _eventSubWebsocketClient.SessionId;
                        subscription.UpdatedAt = DateTime.UtcNow;
                        subDb.EventSubscriptions.Update(subscription);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to subscribe to event {EventType} (ID: {Id}): {Message}",
                            subscription.EventType, subscription.Id, ex.Message);
                    }
                });

                // Save all subscription changes at once
                await subDb.SaveChangesAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to Twitch events: {Message}", ex.Message);
            }
        }
    }

    private async Task OnWebsocketDisconnected(object sender, EventArgs e)
    {
        _logger.LogError($"Websocket {_eventSubWebsocketClient.SessionId} disconnected!");

        // Don't do this in production. You should implement a better reconnect strategy
        while (!await _eventSubWebsocketClient.ReconnectAsync())
        {
            _logger.LogError("Websocket reconnect failed!");
            await Task.Delay(1000);
        }
    }

    private async Task OnWebsocketReconnected(object sender, EventArgs e)
    {
        _logger.LogWarning($"Websocket {_eventSubWebsocketClient.SessionId} reconnected");
    }

    private async Task OnError(object sender, ErrorOccuredArgs args)
    {
        _logger.LogError($"Websocket {_eventSubWebsocketClient.SessionId} - Error occurred!");

        await SaveChannelEvent(
            Guid.NewGuid().ToString(),
            "websocket.error",
            args.Exception
        );

        await Task.CompletedTask;
    }

    // Method to handle event toggling - dynamically subscribe/unsubscribe when events are enabled/disabled
    private async Task HandleEventSubscriptionChange(string eventType, bool enabled)
    {
        _logger.LogInformation("Event subscription changed: {EventType} is now {Status}",
            eventType, enabled ? "enabled" : "disabled");

        // If the websocket is not connected or SessionId is null, we can't subscribe/unsubscribe
        if (!_isConnected || string.IsNullOrEmpty(_eventSubWebsocketClient.SessionId))
        {
            _logger.LogWarning("Cannot modify subscription - WebSocket not connected or SessionId is null");
            return;
        }

        string accessToken = TwitchConfig.Service().AccessToken!;
        string? broadcasterId = _twitchApiService.GetUsers().Result?.FirstOrDefault()?.Id;

        if (string.IsNullOrEmpty(broadcasterId) || string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Cannot modify subscription: Missing broadcaster ID or access token");
            return;
        }

        try
        {
            if (enabled)
            {
                // Get event subscription details from database
                await using AppDbContext enableDb = await _dbContextFactory.CreateDbContextAsync(_cts.Token);
                EventSubscription? subscription = await enableDb.EventSubscriptions
                    .FirstOrDefaultAsync(s => s.Provider == "twitch" && s.EventType == eventType, _cts.Token);

                if (subscription == null)
                {
                    _logger.LogError("Cannot subscribe to event {EventType} - not found in database", eventType);
                    return;
                }

                // Get all conditions for this event type (may be multiple for events like raids)
                List<Dictionary<string, string>> conditions =
                    CreateConditionsForEvent(eventType, broadcasterId, TwitchConfig.Service().ClientId!);

                // Create a separate subscription for each condition set
                foreach (Dictionary<string, string> condition in conditions)
                {
                    await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                        eventType,
                        subscription.Version,
                        condition,
                        EventSubTransportMethod.Websocket,
                        _eventSubWebsocketClient.SessionId,
                        clientId: TwitchConfig.Service().ClientId,
                        accessToken: TwitchConfig.Service().AccessToken);

                    _logger.LogInformation(
                        "Successfully resubscribed to {EventType} (version {Version}) with {Conditions} on session {SessionId}",
                        eventType, subscription.Version ?? "1", 
                        string.Join(", ", condition.Select(kvp => $"{kvp.Key}={kvp.Value}")),
                        _eventSubWebsocketClient.SessionId);
                }

                // Update the SessionId in the database
                subscription.SessionId = _eventSubWebsocketClient.SessionId;
                subscription.UpdatedAt = DateTime.UtcNow;
                enableDb.EventSubscriptions.Update(subscription);
                await enableDb.SaveChangesAsync(_cts.Token);
            }
            else
            {
                // For disabling, we need to find and delete the existing subscription
                // First, check if we have the subscription in our database with the current session ID
                await using AppDbContext disableDb = await _dbContextFactory.CreateDbContextAsync(_cts.Token);
                EventSubscription? subscription = await disableDb.EventSubscriptions
                    .FirstOrDefaultAsync(s => s.Provider == "twitch" && s.EventType == eventType, _cts.Token);

                if (subscription != null)
                {
                    // Get the subscription from Twitch API
                    GetEventSubSubscriptionsResponse? twitchSubscriptions =
                        await _twitchApi.Helix.EventSub.GetEventSubSubscriptionsAsync(
                            type: eventType,
                            accessToken: accessToken);

                    if (twitchSubscriptions != null && twitchSubscriptions.Subscriptions.Any())
                    {
                        // Find subscriptions for this event type that use our current websocket session
                        List<EventSubSubscription> activeSubscriptions = twitchSubscriptions.Subscriptions
                            .Where(s => s.Type == eventType &&
                                        s.Transport.Method == "websocket")
                            .ToList();

                        foreach (EventSubSubscription sub in activeSubscriptions)
                        {
                            // Delete the subscription from Twitch
                            await _twitchApi.Helix.EventSub.DeleteEventSubSubscriptionAsync(
                                sub.Id, accessToken);

                            _logger.LogInformation(
                                "Successfully unsubscribed from {EventType} (Session: {SessionId})",
                                eventType, _eventSubWebsocketClient.SessionId);
                        }

                        // Clear the SessionId in the database to indicate it's no longer active
                        subscription.SessionId = null;
                        subscription.UpdatedAt = DateTime.UtcNow;
                        disableDb.EventSubscriptions.Update(subscription);
                        await disableDb.SaveChangesAsync(_cts.Token);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} event {EventType}: {Message}",
                enabled ? "subscribe to" : "unsubscribe from", eventType, ex.Message);
        }
    }

    // Helper method to create the right conditions for different event types
    // Returns multiple condition dictionaries for events that support multiple subscription directions
    private List<Dictionary<string, string>> CreateConditionsForEvent(string eventType, string broadcasterId, string clientId,
        string? extensionClientId = null)
    {
        List<Dictionary<string, string>> conditions = [];

        _logger.LogDebug("Creating conditions for event type: {EventType}", eventType);

        // Use the condition information directly from AvailableEventTypes if available
        if (TwitchEventSubService.AvailableEventTypes.TryGetValue(eventType,
                out (string, string, string[][] Conditions) eventTypeInfo))
        {
            _logger.LogDebug("Found {ConditionCount} condition sets for event type {EventType}", 
                eventTypeInfo.Conditions.Length, eventType);

            // Each condition array represents a separate subscription
            foreach (string[] conditionParams in eventTypeInfo.Conditions)
            {
                Dictionary<string, string> condition = [];
                
                _logger.LogDebug("Processing condition parameters: {ConditionParams} for event type {EventType}", 
                    string.Join(", ", conditionParams), eventType);
                
                foreach (string conditionParam in conditionParams)
                {
                    switch (conditionParam)
                    {
                        case "broadcaster_user_id":
                            condition["broadcaster_user_id"] = broadcasterId;
                            break;
                        
                        case "to_broadcaster_user_id":
                            condition["to_broadcaster_user_id"] = broadcasterId;
                            break;
                        
                        case "from_broadcaster_user_id":
                            condition["from_broadcaster_user_id"] = TwitchConfig.Service().UserId;
                            break;

                        case "moderator_user_id":
                            condition["moderator_user_id"] = broadcasterId;
                            break;

                        case "client_id":
                            condition["client_id"] = clientId;
                            break;

                        case "user_id":
                            condition["user_id"] = broadcasterId;
                            break;

                        case "extension_client_id":
                            condition["extension_client_id"] = extensionClientId ?? clientId;
                            break;

                        default:
                            _logger.LogWarning("Unknown condition parameter: {ConditionParam} for event type {EventType}",
                                conditionParam, eventType);
                            break;
                    }
                }
                
                _logger.LogDebug("Created condition for event type {EventType}: {Condition}", 
                    eventType, string.Join(", ", condition.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                
                conditions.Add(condition);
            }
        }
        else
        {
            // Fallback in case the event type is not found in the dictionary
            _logger.LogWarning(
                "Event type {EventType} not found in AvailableEventTypes, using broadcaster_user_id as default",
                eventType);
            Dictionary<string, string> fallbackCondition = new() { ["broadcaster_user_id"] = broadcasterId };
            conditions.Add(fallbackCondition);
        }

        _logger.LogDebug("Total conditions created for event type {EventType}: {Count}", eventType, conditions.Count);
        return conditions;
    }

    private async Task SaveChannelEvent(string id, string type, object data, string? channelId = null, string? userId = null)
    {
        _ = await _twitchApiService.GetOrFetchUser(userId);

        await using AppDbContext db = await _dbContextFactory.CreateDbContextAsync();
        await db.ChannelEvents
            .Upsert(new()
            {
                Id = id,
                Type = type,
                Data = data,
                ChannelId = channelId,
                UserId = userId
            })
            .On(p => p.Id)
            .RunAsync();
    }

    // Property to expose current stream for other handlers
    public Stream? CurrentStream => _streamEventHandler.CurrentStream;

    // Method to update current stream reference across handlers
    private void UpdateCurrentStreamReference(Stream? stream)
    {
        _chatEventHandler.SetCurrentStream(stream);
    }
}
