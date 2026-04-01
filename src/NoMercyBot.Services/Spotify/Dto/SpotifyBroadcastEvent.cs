using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public class SpotifyBroadcastEvent : SpotifyGenricEvent<SpotifyBroadcastEventPayload>
{
    [JsonProperty("payloads")]
    public new SpotifyBroadcastEventPayload[]? Payloads { get; set; }

    [JsonProperty("uri")]
    public new string? Uri { get; set; }
}

public class SpotifyBroadcastEventPayload
{
    [JsonProperty("deviceBroadcastStatus")]
    public SpotifyDeviceBroadcastStatus? DeviceBroadcastStatus { get; set; }
}

public class SpotifyDeviceBroadcastStatus
{
    [JsonProperty("timestamp")]
    public string? Timestamp { get; set; }

    [JsonProperty("broadcast_status")]
    public SpotifyEventType? BroadcastStatus { get; set; }

    [JsonProperty("device_id")]
    public string? DeviceId { get; set; }
}

public static class SpotifyBroadcastEventExtensions
{
    public static bool IsSpotifyBroadcastEvent(SpotifyBroadcastEvent data)
    {
        return data.Uri == "social-connect/v2/broadcast_status_update";
    }
}
