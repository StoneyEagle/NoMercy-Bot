using Newtonsoft.Json;

namespace NoMercyBot.Services.Emotes.Dto;

public class ChannelBttvEmotesResponse
{
    [JsonProperty("channelEmotes")]
    public BttvEmote[] ChannelEmotes { get; set; } = [];

    [JsonProperty("sharedEmotes")]
    public BttvEmote[] SharedEmotes { get; set; } = [];
}

public class BttvEmote
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("imageType")]
    public string ImageType { get; set; } = string.Empty;

    [JsonProperty("animated")]
    public bool Animated { get; set; }

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("modifier")]
    public bool Modifier { get; set; }

    [JsonProperty("width", NullValueHandling = NullValueHandling.Ignore)]
    public long? Width { get; set; }

    [JsonProperty("height", NullValueHandling = NullValueHandling.Ignore)]
    public long? Height { get; set; }
}
