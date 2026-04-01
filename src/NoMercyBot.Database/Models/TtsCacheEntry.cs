using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(ContentHash), IsUnique = true)]
public class TtsCacheEntry : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [JsonProperty("id")]
    [StringLength(64)]
    public string ContentHash { get; set; } = string.Empty;

    [StringLength(4000)]
    [JsonProperty("text_content")]
    public string TextContent { get; set; } = string.Empty;

    [StringLength(100)]
    [JsonProperty("voice_id")]
    public string VoiceId { get; set; } = string.Empty;

    [StringLength(50)]
    [JsonProperty("provider")]
    public string Provider { get; set; } = string.Empty;

    [StringLength(500)]
    [JsonProperty("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonProperty("file_size")]
    public long FileSize { get; set; }

    [JsonProperty("character_count")]
    public int CharacterCount { get; set; }

    [Precision(18, 6)]
    [JsonProperty("cost")]
    public decimal Cost { get; set; }

    [JsonProperty("last_accessed_at")]
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("access_count")]
    public int AccessCount { get; set; } = 0;
}
