using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models.ChatMessage;

[NotMapped]
public class ChatCheermote
{
    [JsonProperty("prefix", NullValueHandling = NullValueHandling.Ignore)]
    public string Prefix { get; set; } = string.Empty;

    [JsonProperty("bits", NullValueHandling = NullValueHandling.Ignore)]
    public int Bits { get; set; }

    [JsonProperty("tier", NullValueHandling = NullValueHandling.Ignore)]
    public int Tier { get; set; }

    public ChatCheermote() { }

    public ChatCheermote(TwitchLib.EventSub.Core.Models.Chat.ChatCheermote fragmentCheermote)
    {
        Prefix = fragmentCheermote.Prefix;
        Bits = fragmentCheermote.Bits;
        Tier = fragmentCheermote.Tier;
    }
}
