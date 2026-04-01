using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public class SpotifyState
{
    [JsonProperty("device")]
    public SpotifyDevice? Device { get; set; }

    [JsonProperty("shuffle_state")]
    public bool ShuffleState { get; set; }

    [JsonProperty("repeat_state")]
    public string? RepeatState { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("context")]
    public SpotifyContext? Context { get; set; }

    [JsonProperty("progress_ms")]
    public int ProgressMs { get; set; }

    [JsonProperty("item")]
    public SpotifyItem? Item { get; set; }

    [JsonProperty("currently_playing_type")]
    public string? CurrentlyPlayingType { get; set; }

    [JsonProperty("actions")]
    public SpotifyActions? Actions { get; set; }

    [JsonProperty("is_playing")]
    public bool IsPlaying { get; set; }

    [JsonProperty("is_liked")]
    public bool IsLiked { get; set; }
}

public class SpotifyActions
{
    [JsonProperty("disallows")]
    public SpotifyDisallows? Disallows { get; set; }
}

public class SpotifyDisallows
{
    [JsonProperty("resuming")]
    public bool Resuming { get; set; }

    [JsonProperty("toggling_repeat_context")]
    public bool TogglingRepeatContext { get; set; }

    [JsonProperty("toggling_repeat_track")]
    public bool TogglingRepeatTrack { get; set; }

    [JsonProperty("toggling_shuffle")]
    public bool TogglingShuffle { get; set; }
}

public class SpotifyContext
{
    [JsonProperty("external_urls")]
    public SpotifyExternalUrls? ExternalUrls { get; set; }

    [JsonProperty("href")]
    public string? Href { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("uri")]
    public string? Uri { get; set; }
}

public class SpotifyExternalUrls
{
    [JsonProperty("spotify")]
    public string? Spotify { get; set; }
}

public class SpotifyDevice
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; }

    [JsonProperty("is_private_session")]
    public bool IsPrivateSession { get; set; }

    [JsonProperty("is_restricted")]
    public bool IsRestricted { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("supports_volume")]
    public bool SupportsVolume { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("volume_percent")]
    public int VolumePercent { get; set; }
}

public class SpotifyItem
{
    [JsonProperty("album")]
    public SpotifyAlbum? Album { get; set; }

    [JsonProperty("artists")]
    public SpotifyArtist[]? Artists { get; set; }

    [JsonProperty("disc_number")]
    public int DiscNumber { get; set; }

    [JsonProperty("duration_ms")]
    public int DurationMs { get; set; }

    [JsonProperty("explicit")]
    public bool Explicit { get; set; }

    [JsonProperty("external_ids")]
    public SpotifyExternalIds? ExternalIds { get; set; }

    [JsonProperty("external_urls")]
    public SpotifyExternalUrls? ExternalUrls { get; set; }

    [JsonProperty("href")]
    public string? Href { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("is_local")]
    public bool IsLocal { get; set; }

    [JsonProperty("is_playable")]
    public bool IsPlayable { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("popularity")]
    public int Popularity { get; set; }

    [JsonProperty("preview_url")]
    public string? PreviewUrl { get; set; }

    [JsonProperty("track_number")]
    public int TrackNumber { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("uri")]
    public string? Uri { get; set; }
}

public class SpotifyAlbum
{
    [JsonProperty("album_type")]
    public string? AlbumType { get; set; }

    [JsonProperty("artists")]
    public SpotifyArtist[]? Artists { get; set; }

    [JsonProperty("external_urls")]
    public SpotifyExternalUrls? ExternalUrls { get; set; }

    [JsonProperty("href")]
    public string? Href { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("images")]
    public SpotifyImage[]? Images { get; set; }

    [JsonProperty("is_playable")]
    public bool IsPlayable { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonProperty("release_date_precision")]
    public string? ReleaseDatePrecision { get; set; }

    [JsonProperty("total_tracks")]
    public int TotalTracks { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("uri")]
    public string? Uri { get; set; }
}

public class SpotifyArtist
{
    [JsonProperty("external_urls")]
    public SpotifyExternalUrls? ExternalUrls { get; set; }

    [JsonProperty("href")]
    public string? Href { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("uri")]
    public string? Uri { get; set; }
}

public class SpotifyImage
{
    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("url")]
    public string? Url { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }
}

public class SpotifyExternalIds
{
    [JsonProperty("isrc")]
    public string? Isrc { get; set; }
}

public class SpotifyUser
{
    [JsonProperty("id")]
    public string? Id { get; set; }
}
