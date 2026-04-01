using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class Widget : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(100)]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [MaxLength(20)]
    [JsonProperty("version")]
    public string Version { get; set; } = "1.0.0";

    [MaxLength(20)]
    [JsonProperty("framework")]
    public string Framework { get; set; } = string.Empty; // vue, react, svelte, angular, vanilla

    [JsonProperty("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column(TypeName = "TEXT")]
    [JsonProperty("event_subscriptions")]
    public string EventSubscriptionsJson { get; set; } = "[]";

    [Column(TypeName = "TEXT")]
    [JsonProperty("settings")]
    public string SettingsJson { get; set; } = "{}";

    [NotMapped]
    public List<string> EventSubscriptions
    {
        get => JsonConvert.DeserializeObject<List<string>>(EventSubscriptionsJson) ?? [];
        set => EventSubscriptionsJson = JsonConvert.SerializeObject(value);
    }

    [NotMapped]
    public Dictionary<string, object> Settings
    {
        get =>
            JsonConvert.DeserializeObject<Dictionary<string, object>>(SettingsJson)
            ?? new Dictionary<string, object>();
        set => SettingsJson = JsonConvert.SerializeObject(value);
    }
}
