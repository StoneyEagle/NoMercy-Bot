using Newtonsoft.Json;
using TwitchLib.Api.Helix.Models.Chat.Badges;

namespace NoMercyBot.Services.Twitch.Dto;

public class ChannelResponse
{
    [JsonProperty("data")]
    public List<ChannelData> Data { get; set; } = [];
}

public class ChannelData
{
    [JsonProperty("broadcaster_id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("broadcaster_login")]
    public string BroadCasterLogin { get; set; } = string.Empty;

    [JsonProperty("broadcaster_name")]
    public string BroadcasterName { get; set; } = string.Empty;
}

public class BadgeEmoteSetDto
{
    public string SetId { get; set; } = string.Empty;
    public BadgeVersion[] Versions { get; set; } = [];
    public bool IsGlobalBadge { get; set; }
}
