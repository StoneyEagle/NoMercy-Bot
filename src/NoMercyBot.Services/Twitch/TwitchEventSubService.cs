using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Twitch;

public class TwitchEventSubService : IEventSubService
{
    private readonly ILogger<TwitchEventSubService> _logger;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;

    // Define an event that will notify subscribers about subscription changes
    public delegate Task EventSubscriptionChangedHandler(string eventType, bool enabled);

    public event EventSubscriptionChangedHandler? OnEventSubscriptionChanged;

    public string ProviderName => "twitch";

    public TwitchEventSubService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TwitchEventSubService> logger
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
    }

    internal static readonly Dictionary<
        string,
        (string Description, string Version, string[][] Conditions)
    > AvailableEventTypes = new()
    {
        // Automod events
        {
            "automod.message.hold",
            (
                "A user is notified if a message is caught by automod for review",
                "2",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "automod.message.update",
            (
                "A message in the automod queue had its status changed",
                "2",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "automod.settings.update",
            (
                "A notification is sent when a broadcaster's automod settings are updated",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "automod.terms.update",
            (
                "A notification is sent when a broadcaster's automod terms are updated",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        // Channel events
        {
            "channel.update",
            (
                "A broadcaster updates their channel properties",
                "2",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.follow",
            (
                "A specified channel receives a follow",
                "2",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.ad_break.begin",
            (
                "A midroll commercial break has started running",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.chat.clear",
            (
                "A moderator or bot has cleared all messages from the chat room",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.chat.clear_user_messages",
            (
                "A moderator or bot has cleared all messages from a specific user",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.chat.message",
            (
                "Any user sends a message to a specific chat room",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.chat.message_delete",
            (
                "A moderator has removed a specific message",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.chat.notification",
            (
                "A notification for when an event that appears in chat has occurred",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.chat_settings.update",
            (
                "A notification for when a broadcaster's chat settings are updated",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.chat.user_message_hold",
            (
                "A user is notified if their message is caught by automod",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.chat.user_message_update",
            (
                "A user is notified if their message's automod status is updated",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        {
            "channel.bits.use",
            (
                "A notification is sent whenever Bits are used on a channel",
                "1",
                [
                    ["broadcaster_user_id", "user_id"],
                ]
            )
        },
        // Shared chat events
        {
            "channel.shared_chat.begin",
            (
                "A notification when a channel becomes active in an active shared chat session",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.shared_chat.update",
            (
                "A notification when the active shared chat session the channel is in changes",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.shared_chat.end",
            (
                "A notification when a channel leaves a shared chat session or the session ends",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // Subscription events
        {
            "channel.subscribe",
            (
                "A notification is sent when a specified channel receives a subscriber",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.subscription.end",
            (
                "A notification when a subscription to the specified channel ends",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.subscription.gift",
            (
                "A notification when a viewer gives a gift subscription to one or more users",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.subscription.message",
            (
                "A notification when a user sends a resubscription chat message",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.cheer",
            (
                "A user cheers on the specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // Channel moderation events
        {
            "channel.raid",
            (
                "A broadcaster raids another broadcaster's channel",
                "1",
                [
                    ["to_broadcaster_user_id"],
                    ["from_broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.ban",
            (
                "A viewer is banned from the specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.unban",
            (
                "A viewer is unbanned from the specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.unban_request.create",
            (
                "A user creates an unban request",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.unban_request.resolve",
            (
                "An unban request has been resolved",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.moderate",
            (
                "A moderator performs a moderation action in a channel",
                "2",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.moderator.add",
            (
                "Moderator privileges were added to a user on a specified channel",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.moderator.remove",
            (
                "Moderator privileges were removed from a user on a specified channel",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.vip.add",
            (
                "A VIP is added to the channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.vip.remove",
            (
                "A VIP is removed from the channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.warning.acknowledge",
            (
                "A user acknowledges a warning",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.warning.send",
            (
                "A user is sent a warning",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        // Guest Star events
        {
            "channel.guest_star_session.begin",
            (
                "The host began a new Guest Star session",
                "beta",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.guest_star_session.end",
            (
                "A running Guest Star session has ended",
                "beta",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.guest_star_guest.update",
            (
                "A guest or a slot is updated in an active Guest Star session",
                "beta",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.guest_star_settings.update",
            (
                "The host preferences for Guest Star have been updated",
                "beta",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        // Channel points events
        {
            "channel.channel_points_automatic_reward_redemption.add",
            (
                "A viewer has redeemed an automatic channel points reward",
                "2",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.channel_points_custom_reward.add",
            (
                "A custom channel points reward has been created",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.channel_points_custom_reward.update",
            (
                "A custom channel points reward has been updated",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.channel_points_custom_reward.remove",
            (
                "A custom channel points reward has been removed",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.channel_points_custom_reward_redemption.add",
            (
                "A viewer has redeemed a custom channel points reward",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.channel_points_custom_reward_redemption.update",
            (
                "A redemption of a channel points custom reward has been updated",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // Poll events
        {
            "channel.poll.begin",
            (
                "A poll started on a specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.poll.progress",
            (
                "Users respond to a poll on a specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.poll.end",
            (
                "A poll ended on a specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // Prediction events
        {
            "channel.prediction.begin",
            (
                "A Prediction started on a specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.prediction.progress",
            (
                "Users participated in a Prediction on a specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.prediction.lock",
            (
                "A Prediction was locked on a specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.prediction.end",
            (
                "A Prediction ended on a specified channel",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // Suspicious user events
        {
            "channel.suspicious_user.message",
            (
                "A chat message has been sent by a suspicious user",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.suspicious_user.update",
            (
                "A suspicious user has been updated",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        // Charity events
        {
            "channel.charity_campaign.donate",
            (
                "A user donates to the broadcaster's charity campaign",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.charity_campaign.start",
            (
                "The broadcaster starts a charity campaign",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.charity_campaign.progress",
            (
                "Progress is made towards the campaign's goal",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.charity_campaign.stop",
            (
                "The broadcaster stops a charity campaign",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // Conduit events
        // {
        //     "conduit.shard.disabled",
        // ("EventSub disables a shard due to transport status changing", "1",
        //         [["client_id"]])
        // },

        // Drop events (not supported by websockets yet, but kept for IEventSubService implementation)
        // {
        //     "drop.entitlement.grant",
        //     ("An entitlement for a Drop is granted to a user", "1",
        // [["broadcaster_user_id", "moderator_user_id"]])
        // },

        // Extension events
        {
            "extension.bits_transaction.create",
            (
                "A Bits transaction occurred for a Twitch Extension",
                "1",
                [
                    ["extension_client_id"],
                ]
            )
        },
        // Goal events
        {
            "channel.goal.begin",
            (
                "A broadcaster begins a goal",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.goal.progress",
            (
                "Progress is made towards a broadcaster's goal",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.goal.end",
            (
                "A broadcaster ends a goal",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // Hype Train events
        {
            "channel.hype_train.begin",
            (
                "A Hype Train begins on the specified channel",
                "2",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.hype_train.progress",
            (
                "A Hype Train makes progress on the specified channel",
                "2",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "channel.hype_train.end",
            (
                "A Hype Train ends on the specified channel",
                "2",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // Shield Mode events
        {
            "channel.shield_mode.begin",
            (
                "The broadcaster activates Shield Mode",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.shield_mode.end",
            (
                "The broadcaster deactivates Shield Mode",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        // Shoutout events
        {
            "channel.shoutout.create",
            (
                "The specified broadcaster sends a Shoutout",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        {
            "channel.shoutout.receive",
            (
                "The specified broadcaster receives a Shoutout",
                "1",
                [
                    ["broadcaster_user_id", "moderator_user_id"],
                ]
            )
        },
        // Stream events
        {
            "stream.online",
            (
                "The specified broadcaster starts a stream",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        {
            "stream.offline",
            (
                "The specified broadcaster stops a stream",
                "1",
                [
                    ["broadcaster_user_id"],
                ]
            )
        },
        // User events
        // {
        //     "user.authorization.grant",
        //     ("A user's authorization has been granted to your client id", "1",
        // [["client_id"]])
        // },
        // {
        //     "user.authorization.revoke",
        // ("A user's authorization has been revoked for your client id", "1",
        // [["client_id"]])
        // },
        {
            "user.update",
            (
                "A user has updated their account",
                "1",
                [
                    ["user_id"],
                ]
            )
        },
        {
            "user.whisper.message",
            (
                "A user receives a whisper",
                "1",
                [
                    ["user_id"],
                ]
            )
        },
    };

    // These methods are no longer used for websockets but kept for IEventSubService implementation
    public bool VerifySignature(HttpRequest request, string payload)
    {
        _logger.LogWarning("VerifySignature was called but is not used with websockets");
        return true; // Not used with websockets
    }

    public async Task<IActionResult> HandleEventAsync(
        HttpRequest request,
        string payload,
        string eventType
    )
    {
        _logger.LogWarning(
            "HandleEventAsync was called but is not used with websockets. Events are handled by TwitchWebsocketHostedService"
        );
        return new OkResult(); // Not used with websockets
    }

    private async Task ProcessEventNotification(string payload, string eventType)
    {
        _logger.LogWarning("ProcessEventNotification was called but is not used with websockets");
        await Task.CompletedTask; // Not used with websockets
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

    // Modified to handle websocket subscriptions
    public async Task<EventSubscription> CreateSubscriptionAsync(
        string eventType,
        bool enabled = true
    )
    {
        // Check if event type is valid
        if (!AvailableEventTypes.ContainsKey(eventType))
            throw new ArgumentException($"Invalid event type: {eventType}");

        // Check if subscription already exists in the database
        EventSubscription? existingSub = await _dbContext.EventSubscriptions.FirstOrDefaultAsync(
            s => s.Provider == ProviderName && s.EventType == eventType
        );

        if (existingSub != null)
        {
            // Only update and notify if the enabled state is actually changing
            bool stateChanged = existingSub.Enabled != enabled;

            existingSub.Enabled = enabled;
            existingSub.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Notify subscribers about the change if the state changed
            if (stateChanged && OnEventSubscriptionChanged != null)
            {
                _logger.LogInformation(
                    "Notifying about subscription change: {EventType} is now {Status}",
                    eventType,
                    enabled ? "enabled" : "disabled"
                );
                await OnEventSubscriptionChanged(eventType, enabled);
            }

            return existingSub;
        }

        // Create new subscription in database
        EventSubscription subscription = new(
            ProviderName,
            eventType,
            enabled,
            AvailableEventTypes[eventType].Version
        )
        {
            Description = AvailableEventTypes[eventType].Description,
        };

        await _dbContext.EventSubscriptions.AddAsync(subscription);
        await _dbContext.SaveChangesAsync();

        // Notify subscribers about the new subscription if it's enabled
        if (enabled && OnEventSubscriptionChanged != null)
        {
            _logger.LogInformation(
                "Notifying about new subscription: {EventType} is now enabled",
                eventType
            );
            await OnEventSubscriptionChanged(eventType, true);
        }

        return subscription;
    }

    public async Task UpdateSubscriptionAsync(string id, bool enabled)
    {
        EventSubscription? subscription = await _dbContext.EventSubscriptions.FirstOrDefaultAsync(
            s => s.Provider == ProviderName && s.Id == id
        );

        if (subscription == null)
            throw new KeyNotFoundException($"Subscription not found: {id}");

        // Only process if the enabled state is actually changing
        if (subscription.Enabled != enabled)
        {
            subscription.Enabled = enabled;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            // Notify subscribers about the change
            if (OnEventSubscriptionChanged != null)
            {
                _logger.LogInformation(
                    "Notifying about subscription update: {EventType} is now {Status}",
                    subscription.EventType,
                    enabled ? "enabled" : "disabled"
                );
                await OnEventSubscriptionChanged(subscription.EventType, enabled);
            }
        }
    }

    public async Task UpdateAllSubscriptionsAsync(EventSubscription[] subscriptions)
    {
        if (subscriptions == null || subscriptions.Length == 0)
            throw new ArgumentException("No subscriptions provided to update");

        // Track which subscriptions had their enabled status changed
        Dictionary<string, bool> changedSubscriptions = new();

        // First pass - detect all changes before modifying any entities
        foreach (EventSubscription sub in subscriptions)
        {
            if (sub.Provider != ProviderName)
                throw new ArgumentException($"Invalid provider for subscription: {sub.Id}");

            // Check if enabled status is changing
            EventSubscription? existingSub = await _dbContext
                .EventSubscriptions.AsNoTracking() // Use AsNoTracking to avoid change tracking conflicts
                .FirstOrDefaultAsync(s => s.Id == sub.Id);

            if (existingSub != null && existingSub.Enabled != sub.Enabled)
            {
                _logger.LogInformation(
                    "Detected subscription change: {EventType} will be {Status}",
                    sub.EventType,
                    sub.Enabled ? "enabled" : "disabled"
                );
                changedSubscriptions[sub.EventType] = sub.Enabled;
            }
        }

        // Second pass - apply the updates to the database
        foreach (EventSubscription sub in subscriptions)
            _dbContext.EventSubscriptions.Update(sub);

        await _dbContext.SaveChangesAsync();

        // Process any subscription status changes by triggering the event
        if (OnEventSubscriptionChanged != null && changedSubscriptions.Count > 0)
        {
            _logger.LogInformation($"Processing {changedSubscriptions.Count} subscription changes");
            foreach (KeyValuePair<string, bool> kvp in changedSubscriptions)
            {
                _logger.LogInformation(
                    "Notifying about batch subscription change: {EventType} is now {Status}",
                    kvp.Key,
                    kvp.Value ? "enabled" : "disabled"
                );
                await OnEventSubscriptionChanged(kvp.Key, kvp.Value);
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

        bool wasEnabled = subscription.Enabled;
        string eventType = subscription.EventType;

        _dbContext.EventSubscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync();

        // If the subscription was enabled, notify that it's now disabled
        if (wasEnabled && OnEventSubscriptionChanged != null)
        {
            _logger.LogInformation(
                "Notifying about deleted subscription: {EventType} is now disabled",
                eventType
            );
            await OnEventSubscriptionChanged(eventType, false);
        }
    }

    public async Task<bool> DeleteAllSubscriptionsAsync()
    {
        try
        {
            // Get all subscriptions for this provider
            List<EventSubscription> subscriptions = await _dbContext
                .EventSubscriptions.Where(s => s.Provider == ProviderName)
                .ToListAsync();

            // For each enabled subscription, notify that it's being disabled
            foreach (EventSubscription sub in subscriptions.Where(s => s.Enabled))
                if (OnEventSubscriptionChanged != null)
                {
                    _logger.LogInformation(
                        "Notifying about bulk deleted subscription: {EventType} is now disabled",
                        sub.EventType
                    );
                    await OnEventSubscriptionChanged(sub.EventType, false);
                }

            // Delete from our database
            _dbContext.EventSubscriptions.RemoveRange(subscriptions);
            await _dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all subscriptions");
            return false;
        }
    }

    public IEnumerable<string> GetAvailableEventTypes()
    {
        return AvailableEventTypes.Keys;
    }

    private static string ComputeHmac256(string secretKey, string message)
    {
        byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);

        using HMACSHA256 hmac = new(secretKeyBytes);
        byte[] hashBytes = hmac.ComputeHash(messageBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }
}
