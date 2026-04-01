using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class Stream : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [MaxLength(50)]
    [JsonProperty("language")]
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

    [JsonProperty("content_labels")]
    public List<string> ContentLabels { get; set; } = [];

    [JsonProperty("is_branded_content")]
    public bool IsBrandedContent { get; set; }

    [JsonProperty("channel_id")]
    public string ChannelId { get; set; } = null!;

    [JsonProperty("channel")]
    public virtual Channel Channel { get; set; } = new();
}
