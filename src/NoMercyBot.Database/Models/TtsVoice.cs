using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class TtsVoice : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("speaker_id")]
    public string SpeakerId { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonProperty("locale")]
    public string Locale { get; set; } = string.Empty;

    [JsonProperty("gender")]
    public string Gender { get; set; } = string.Empty;

    [JsonProperty("age")]
    public int Age { get; set; }

    [JsonProperty("accent")]
    public string Accent { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Region { get; set; } = string.Empty;

    [JsonProperty("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonProperty("is_default")]
    public bool IsDefault { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("user_tts_voices")]
    public ICollection<UserTtsVoice> UserTtsVoices { get; set; } = [];
}
