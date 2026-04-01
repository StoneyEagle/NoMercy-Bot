using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models.ChatMessage;

[NotMapped]
public class ChatEmote
{
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("emote_set_id", NullValueHandling = NullValueHandling.Ignore)]
    public string EmoteSetId { get; set; } = string.Empty;

    [JsonProperty("owner_id", NullValueHandling = NullValueHandling.Ignore)]
    public string OwnerId { get; set; } = string.Empty;

    [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
    public string[] Format { get; set; } = [];

    [JsonProperty("provider", NullValueHandling = NullValueHandling.Ignore)]
    public string Provider { get; set; }

    [JsonProperty("urls", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, Uri> Urls { get; set; }

    [JsonProperty("is_gigantified", NullValueHandling = NullValueHandling.Ignore)]
    public bool IsGigantified { get; set; }

    public ChatEmote() { }

    public ChatEmote(TwitchLib.EventSub.Core.Models.Chat.ChatEmote fragmentEmote)
    {
        Id = fragmentEmote.Id;
        EmoteSetId = fragmentEmote.EmoteSetId;
        OwnerId = fragmentEmote.OwnerId;
        Format = fragmentEmote.Format ?? [];
    }
}
