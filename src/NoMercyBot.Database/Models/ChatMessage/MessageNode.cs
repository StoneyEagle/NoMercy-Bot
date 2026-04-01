using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models.ChatMessage;

[NotMapped]
public class MessageNode
{
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string Type { get; set; } = null!;

    private string Index { get; set; } = string.Empty;

    [Column("Id")]
    [StringLength(1024)]
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _id { get; set; } = string.Empty;

    [NotMapped]
    public string Id
    {
        get => _id;
        set => _id = $"{value}_{Type}_{Index}";
    }

    [JsonProperty("classes", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? Classes { get; set; }

    [JsonProperty("children", NullValueHandling = NullValueHandling.Ignore)]
    public List<MessageNode>? Children { get; set; }

    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string? Text { get; set; }

    [JsonProperty("attribs", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string> Attribs { get; set; } = new();
}
