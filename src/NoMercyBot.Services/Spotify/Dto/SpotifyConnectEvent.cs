using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public class SpotifyConnectEvent : SpotifyGenricEvent<object>
{
    [JsonProperty("headers")]
    public new SpotifyConnectHeaders? Headers { get; set; }

    [JsonProperty("method")]
    public SpotifyEventType Method { get; set; }

    [JsonProperty("type")]
    public new string Type { get; set; } = "message";

    [JsonProperty("uri")]
    public new string? Uri { get; set; }
}

public class SpotifyConnectHeaders
{
    [JsonProperty("Spotify-Connection-Id")]
    public string? SpotifyConnectionId { get; set; }
}

public static class SpotifyConnectEventExtensions
{
    public static bool IsSpotifyConnectEvent(SpotifyConnectEvent data)
    {
        return data.Uri != null && data.Uri.StartsWith("hm://pusher/v1/connections/");
    }
}
