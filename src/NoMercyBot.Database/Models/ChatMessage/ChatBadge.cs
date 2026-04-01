using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models.ChatMessage;

[NotMapped]
public class ChatBadge
{
    [JsonProperty("set_id")]
    public string? SetId { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("info")]
    public string? Info { get; set; }

    [JsonProperty("urls")]
    public Dictionary<string, Uri> Urls { get; set; } = new();
}
