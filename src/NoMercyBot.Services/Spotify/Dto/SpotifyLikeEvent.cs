using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public class SpotifyLikeEvent : SpotifyGenricEvent<string>
{
    [JsonProperty("payloads")]
    public new string[]? Payloads { get; set; }

    [JsonProperty("uri")]
    public string? Uri { get; set; }

    [JsonProperty("type")]
    public string? EventType { get; set; }

    [JsonProperty("payload")]
    public SpotifyLikePayload? Payload { get; set; }
}

public class SpotifyLikePayload
{
    [JsonProperty("items")]
    public SpotifyLikeItem[]? Items { get; set; }
}

public class SpotifyLikeItem
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("unheard")]
    public bool Unheard { get; set; }

    [JsonProperty("addedAt")]
    public long AddedAt { get; set; }

    [JsonProperty("removed")]
    public bool Removed { get; set; }

    [JsonProperty("identifier")]
    public string? Identifier { get; set; }
}

public static class SpotifyLikeEventExtensions
{
    public static bool IsSpotifyLikeEvent(SpotifyLikeEvent data)
    {
        return data.Uri != null && data.Uri.StartsWith("hm://collection/collection/");
    }
}
