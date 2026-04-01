using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models.ChatMessage;

[NotMapped]
public class HtmlPreviewCustomContent
{
    [JsonProperty("host", NullValueHandling = NullValueHandling.Ignore)]
    public required string Host { get; set; }

    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? ImageUrl { get; set; }
}
