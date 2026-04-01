using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public class SpotifyMessageEvent : SpotifyGenricEvent<SpotifyMessageEventPayload>
{
    [JsonProperty("payloads")]
    public new SpotifyMessageEventPayload[] Payloads { get; set; } = [];
}

public class SpotifyMessageEventPayload
{
    [JsonProperty("events")]
    public SpotifyEventElement[] Events { get; set; } = [];

    [JsonProperty("uri")]
    public string? Uri { get; set; }
}

public class SpotifyEventElement
{
    [JsonProperty("source")]
    public string? Source { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("uri")]
    public string? Uri { get; set; }

    [JsonProperty("href")]
    public Uri? Href { get; set; }

    [JsonProperty("event")]
    public SpotifyEventEvent? Event { get; set; }

    [JsonProperty("user")]
    public SpotifyUser? User { get; set; }
}

public class SpotifyEventEvent
{
    [JsonProperty("event_id")]
    public long EventId { get; set; }

    [JsonProperty("state")]
    public SpotifyState? State { get; set; }
}

public static class SpotifyMessageEventExtensions
{
    public static bool IsSpotifyMessageEvent(SpotifyMessageEvent data)
    {
        return data.Uri == "wss://event";
    }
}
