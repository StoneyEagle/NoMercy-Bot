using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Provider), nameof(EventType), nameof(Condition), IsUnique = true)]
public class EventSubscription : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public string Id { get; set; } = Ulid.NewUlid().ToString();

    [JsonProperty("provider")]
    public string Provider { get; set; } = null!;

    [JsonProperty("event_type")]
    public string EventType { get; set; } = null!;

    [JsonProperty("description")]
    public string Description { get; set; } = null!;

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("version")]
    public string? Version { get; set; }

    [JsonProperty("subscription_id")]
    public string? SubscriptionId { get; set; }

    [JsonProperty("session_id")]
    public string? SessionId { get; set; }

    [JsonProperty("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    [JsonProperty("condition")]
    public string[] Condition { get; set; } = [];

    public EventSubscription() { }

    public EventSubscription(
        string provider,
        string eventType,
        bool enabled = true,
        string? version = null
    )
    {
        Id = Ulid.NewUlid().ToString();
        Provider = provider;
        EventType = eventType;
        Enabled = enabled;
        Version = version;
    }
}
