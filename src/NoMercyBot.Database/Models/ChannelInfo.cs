using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class ChannelInfo : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(50)]
    [JsonProperty("broadcaster_id")]
    public string Id { get; set; } = null!;

    [JsonProperty("is_live")]
    public bool IsLive { get; set; }

    [MaxLength(50)]
    [JsonProperty("broadcaster_language")]
    public string Language { get; set; } = string.Empty;

    [MaxLength(50)]
    [JsonProperty("game_id")]
    public string GameId { get; set; } = string.Empty;

    [MaxLength(255)]
    [JsonProperty("game_name")]
    public string GameName { get; set; } = string.Empty;

    [MaxLength(255)]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("delay")]
    public int Delay { get; set; }

    [JsonProperty("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonProperty("content_classification_labels")]
    public List<string> ContentLabels { get; set; } = [];

    [JsonProperty("is_branded_content")]
    public bool IsBrandedContent { get; set; }
}
