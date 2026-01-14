using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models.ChatMessage;

[NotMapped]
public class ChatMention
{
    [JsonProperty("user_id", NullValueHandling = NullValueHandling.Ignore)]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("user_name", NullValueHandling = NullValueHandling.Ignore)]
    public string UserName { get; set; } = string.Empty;

    [JsonProperty("user_login", NullValueHandling = NullValueHandling.Ignore)]
    public string UserLogin { get; set; } = string.Empty;

    [JsonProperty("color_hex", NullValueHandling = NullValueHandling.Ignore)]
    public string ColorHex { get; set; } = null!;

    public ChatMention()
    {
    }

    public ChatMention(TwitchLib.EventSub.Core.Models.Chat.ChatMention fragmentMention)
    {
        AppDbContext context = new();
        string colorHex = context.Users.FirstOrDefault(u => u.Id == fragmentMention.UserId)?.Color ?? "#ffffff";
        
        UserId = fragmentMention.UserId;
        UserName = fragmentMention.UserName;
        UserLogin = fragmentMention.UserLogin;
        ColorHex = colorHex;
    }
}