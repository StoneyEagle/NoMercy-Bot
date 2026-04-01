using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TwitchLibMetadata = TwitchLib.EventSub.Core.Models.EventSubMetadata;

namespace NoMercyBot.Services.Twitch.Models;

/// <summary>
/// EventSubMetadata for handling Twitch EventSub notification metadata.
/// </summary>
public class EventSubMetadata
{
    /// <summary>
    /// An ID that uniquely identifies message.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// The type of notification.
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// The UTC date and time that Twitch sent the notification.
    /// </summary>
    public DateTime MessageTimestamp { get; set; }

    /// <summary>
    /// The subscription type.
    /// </summary>
    public string? SubscriptionType { get; set; }

    /// <summary>
    /// The subscription version.
    /// </summary>
    public string? SubscriptionVersion { get; set; }

#if NET8_0_OR_GREATER
    [MemberNotNullWhen(true, nameof(SubscriptionType), nameof(SubscriptionVersion))]
#endif
    public bool HasSubscriptionInfo =>
        SubscriptionType is not null && SubscriptionVersion is not null;
}

/// <summary>
/// Extension methods for TwitchLib EventSubMetadata to access internal properties.
/// </summary>
public static class EventSubMetadataExtensions
{
    /// <summary>
    /// Gets the MessageId from TwitchLib's EventSubMetadata using reflection.
    /// </summary>
    public static string GetMessageId(this TwitchLibMetadata metadata)
    {
        // Get property from the actual runtime type, not the base type
        PropertyInfo? property = metadata
            .GetType()
            .GetProperty(
                "MessageId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
        return property?.GetValue(metadata) as string ?? string.Empty;
    }
}
