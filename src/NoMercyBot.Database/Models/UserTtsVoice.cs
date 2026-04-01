using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(UserId), IsUnique = true)]
public class UserTtsVoice
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("tts_voice_id")]
    public string TtsVoiceId { get; set; } = string.Empty;

    [JsonProperty("tts_voice")]
    public TtsVoice TtsVoice { get; set; } = new();

    [JsonProperty("set_at")]
    public DateTime SetAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("user_id")]
    public string UserId { get; set; } = null!;

    [JsonProperty("user")]
    public User User { get; set; } = new();
}
