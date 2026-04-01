using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Discord;

public class DiscordEventSubService : IEventSubService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DiscordEventSubService> _logger;
    private readonly DiscordApiService _discordApiService;

    public string ProviderName => "discord";

    internal static readonly Dictionary<string, string> AvailableEventTypes = new()
    {
        { "guild.create", "When the bot joins a new Discord server" },
        { "guild.delete", "When the bot leaves or is removed from a Discord server" },
        { "guild.member_add", "When a new member joins a Discord server" },
        { "guild.member_remove", "When a member leaves or is removed from a Discord server" },
        { "message.create", "When a message is sent in a channel" },
        { "message.delete", "When a message is deleted" },
        { "voice.state_update", "When a user joins, leaves, or moves between voice channels" },
        { "interaction", "When a user interacts with a bot command or component" },
        { "ready", "When the bot has successfully connected to Discord" },
        { "channel.create", "When a new channel is created" },
        { "channel.delete", "When a channel is deleted" },
        { "channel.pins_update", "When a message is pinned or unpinned in a channel" },
        { "guild.ban_add", "When a user is banned from a server" },
        { "guild.ban_remove", "When a user is unbanned from a server" },
        { "guild.emojis_update", "When a server's emoji list is updated" },
        { "guild.integrations_update", "When a guild integration is updated" },
        { "guild.role_create", "When a role is created in a server" },
        { "guild.role_delete", "When a role is deleted from a server" },
        { "guild.role_update", "When a role's settings are updated" },
    };

    public DiscordEventSubService(
        ILogger<DiscordEventSubService> logger,
        IServiceScopeFactory serviceScopeFactory,
        DiscordApiService discordApiService
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _discordApiService = discordApiService;
    }

    public bool VerifySignature(HttpRequest request, string payload)
    {
        try
        {
            if (
                !request.Headers.TryGetValue(
                    "X-Signature-Ed25519",
                    out StringValues signatureValues
                )
                || !request.Headers.TryGetValue(
                    "X-Signature-Timestamp",
                    out StringValues timestampValues
                )
            )
                return false;

            string signature = signatureValues.ToString();
            string timestamp = timestampValues.ToString();

            // Discord uses Ed25519 for signature verification, which is different from Twitch's HMAC-SHA256
            // This is a placeholder - Discord verification would need the proper Ed25519 implementation
            // using Discord's public key (from their developer portal)
            return true; // Replace with actual verification
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Discord signature");
            return false;
        }
    }

    public async Task<IActionResult> HandleEventAsync(
        HttpRequest request,
        string payload,
        string eventType
    )
    {
        try
        {
            // Discord uses an "interactions" endpoint for most webhooks
            // This is simplified - actual Discord interaction handling would be more complex
            JObject? json = payload.FromJson<JObject>();

            if (json == null)
                return new BadRequestObjectResult("Invalid payload");

            int? type = (int?)json["type"];

            // Handle Discord interaction types
            switch (type)
            {
                case 1: // PING
                    // Discord sends a ping to verify endpoint
                    return new OkObjectResult(new { type = 1 }); // Respond with PONG

                case 2: // APPLICATION_COMMAND
                    await ProcessCommandInteraction(json);
                    return new OkObjectResult(
                        new { type = 4, data = new { content = "Command received!" } }
                    );

                default:
                    // Process other Discord events based on eventType
                    await ProcessDiscordEvent(payload, eventType);
                    return new OkResult();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Discord event");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    private Task ProcessCommandInteraction(JObject json)
    {
        try
        {
            string? commandName = json["data"]?["name"]?.ToString();
            _logger.LogInformation("Received Discord command: {CommandName}", commandName);

            // Handle different commands
            // This would contain your Discord command implementation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Discord command interaction");
        }

        return Task.CompletedTask;
    }

    private Task ProcessDiscordEvent(string payload, string eventType)
    {
        try
        {
            // Process different Discord events based on eventType
            _logger.LogInformation("Processing Discord event: {EventType}", eventType);

            // This would contain event-specific logic
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Discord event");
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
            return existingSub;
        }

        // Create new subscription
        // Note: Discord doesn't have a direct API for subscribing to events like Twitch does
        // Instead, you configure webhooks in the Discord Developer Portal or use the API
        // to create webhooks for specific channels

        EventSubscription subscription = new(ProviderName, eventType, enabled);

        await _dbContext.EventSubscriptions.AddAsync(subscription);
        await _dbContext.SaveChangesAsync();

        return subscription;
    }

    public async Task UpdateSubscriptionAsync(string id, bool enabled)
    {
        EventSubscription? subscription = await _dbContext.EventSubscriptions.FirstOrDefaultAsync(
            s => s.Provider == ProviderName && s.Id == id
        );

        if (subscription == null)
            throw new KeyNotFoundException($"Subscription not found: {id}");

        subscription.Enabled = enabled;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
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

    public async Task DeleteSubscriptionAsync(string id)
    {
        EventSubscription? subscription = await _dbContext.EventSubscriptions.FirstOrDefaultAsync(
            s => s.Provider == ProviderName && s.Id == id
        );

        if (subscription == null)
            return;

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

            _dbContext.EventSubscriptions.RemoveRange(subscriptions);
            await _dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all Discord subscriptions");
            return false;
        }
    }

    public IEnumerable<string> GetAvailableEventTypes()
    {
        return AvailableEventTypes.Keys;
    }
}
