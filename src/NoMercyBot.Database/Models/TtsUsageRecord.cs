using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class TtsUsageRecord : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [MaxLength(50)]
    [JsonProperty("provider_id")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonProperty("characters_used")]
    public int CharactersUsed { get; set; }

    [JsonProperty("cost")]
    public decimal Cost { get; set; }

    [JsonProperty("billing_period_start")]
    public DateTime BillingPeriodStart { get; set; }

    [JsonProperty("billing_period_end")]
    public DateTime BillingPeriodEnd { get; set; }

    [ForeignKey(nameof(ProviderId))]
    [JsonProperty("provider")]
    public TtsProvider? Provider { get; set; }
}
