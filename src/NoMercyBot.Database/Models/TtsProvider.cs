using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class TtsProvider : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonProperty("monthly_character_limit")]
    public int MonthlyCharacterLimit { get; set; } = 500000; // Default 500k characters

    [Precision(18, 8)]
    [JsonProperty("cost_per_character")]
    public decimal CostPerCharacter { get; set; } = 0.000016m; // Azure TTS pricing

    [JsonProperty("max_characters_per_request")]
    public int MaxCharactersPerRequest { get; set; } = 3000;

    [JsonProperty("priority")]
    public int Priority { get; set; } = 1;

    [StringLength(4000)]
    [JsonProperty("configuration_json")]
    public string? ConfigurationJson { get; set; }

    // Navigation properties
    [JsonProperty("usage_records")]
    public ICollection<TtsUsageRecord> UsageRecords { get; set; } = [];
}
