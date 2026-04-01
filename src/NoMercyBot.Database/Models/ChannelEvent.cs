using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class ChannelEvent : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [MaxLength(50)]
    [JsonProperty("type")]
    public string Type { get; set; } = null!;

    [JsonProperty("data")]
    public object? Data { get; set; }

    [JsonProperty("channel_id")]
    public string? ChannelId { get; set; }

    [JsonProperty("channel")]
    public Channel Channel { get; set; } = null!;

    [JsonProperty("user_id")]
    public string? UserId { get; set; }

    [JsonProperty("user")]
    public User User { get; set; } = null!;
}
