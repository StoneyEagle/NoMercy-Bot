using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models.ChatMessage;

[NotMapped]
public class ChatMessageFragment
{
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("cheermote", NullValueHandling = NullValueHandling.Ignore)]
    public ChatCheermote? Cheermote { get; set; }

    [JsonProperty("emote", NullValueHandling = NullValueHandling.Ignore)]
    public ChatEmote? Emote { get; set; }

    [JsonProperty("mention", NullValueHandling = NullValueHandling.Ignore)]
    public ChatMention? Mention { get; set; }

    [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
    public string? Command { get; set; }

    [JsonProperty("args", NullValueHandling = NullValueHandling.Ignore)]
    public string[]? Args { get; set; }

    [JsonProperty("html_preview", NullValueHandling = NullValueHandling.Ignore)]
    public HtmlPreviewCustomContent? HtmlContent { get; set; }

    public ChatMessageFragment() { }

    public ChatMessageFragment(TwitchLib.EventSub.Core.Models.Chat.ChatMessageFragment fragment)
    {
        Type = fragment.Type;
        Text = fragment.Text;

        if (fragment.Cheermote != null)
            Cheermote = new(fragment.Cheermote);

        if (fragment.Emote != null)
            Emote = new(fragment.Emote);

        if (fragment.Mention != null)
            Mention = new(fragment.Mention);
    }
}
