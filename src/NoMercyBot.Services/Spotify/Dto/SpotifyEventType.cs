using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public enum SpotifyEventType
{
    PLAYER_STATE_CHANGED,
    BroadcastUnavailable,
    Put,
}

public class SpotifyHeaders
{
    [JsonProperty("content-type")]
    public string? ContentType { get; set; }

    [JsonProperty("Spotify-Connection-Id")]
    public string? SpotifyConnectionId { get; set; }
}

public abstract class SpotifyGenricEvent<T>
{
    [JsonProperty("headers")]
    public SpotifyHeaders? Headers { get; set; }

    [JsonProperty("payloads")]
    public T[]? Payloads { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("uri")]
    public string? Uri { get; set; }
}
