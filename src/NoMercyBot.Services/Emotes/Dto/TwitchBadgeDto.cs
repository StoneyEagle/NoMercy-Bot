using Newtonsoft.Json;

namespace NoMercyBot.Services.Emotes.Dto;

public class TwitchGlobalBadgesResponse
{
    [JsonProperty("data")]
    public TwitchGlobalBadgesResponseData[] Data { get; set; } = [];
}

public class TwitchGlobalBadgesResponseData
{
    [JsonProperty("set_id")]
    public string SetId { get; set; } = null!;

    [JsonProperty("versions")]
    public TwitchGlobalBadgesVersion[] Versions { get; set; } = [];
}

public class TwitchGlobalBadgesVersion
{
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("image_url_1x")]
    public Uri ImageUrl1X { get; set; } = null!;

    [JsonProperty("image_url_2x")]
    public Uri ImageUrl2X { get; set; } = null!;

    [JsonProperty("image_url_4x")]
    public Uri ImageUrl4X { get; set; } = null!;

    [JsonProperty("title")]
    public string Title { get; set; } = null!;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("click_action")]
    public string? ClickAction { get; set; }

    [JsonProperty("click_url")]
    public Uri? ClickUrl { get; set; }
}
