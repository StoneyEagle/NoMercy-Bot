using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public class CurrentlyPlaying
{
    [JsonProperty("is_playing")]
    public bool IsPlaying { get; set; }

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("context")]
    public Context Context { get; set; } = new();

    [JsonProperty("progress_ms")]
    public long ProgressMs { get; set; }

    [JsonProperty("item")]
    public Item Item { get; set; } = new();

    [JsonProperty("currently_playing_type")]
    public string CurrentlyPlayingType { get; set; } = null!;

    [JsonProperty("actions")]
    public Actions Actions { get; set; } = new();
}

public class Actions
{
    [JsonProperty("disallows")]
    public Disallows Disallows { get; set; } = new();
}

public class Disallows
{
    [JsonProperty("resuming")]
    public bool Resuming { get; set; }
}

public class Context
{
    [JsonProperty("external_urls")]
    public ExternalUrls ExternalUrls { get; set; } = new();

    [JsonProperty("href")]
    public Uri Href { get; set; } = null!;

    [JsonProperty("type")]
    public string Type { get; set; } = null!;

    [JsonProperty("uri")]
    public string Uri { get; set; } = null!;
}

public class Item
{
    private Uri _href;

    [JsonProperty("album")]
    public Album Album { get; set; } = new();

    [JsonProperty("artists")]
    public Artist[] Artists { get; set; } = [];

    [JsonProperty("available_markets")]
    public string[] AvailableMarkets { get; set; } = [];

    [JsonProperty("disc_number")]
    public long DiscNumber { get; set; }

    [JsonProperty("duration_ms")]
    public long DurationMs { get; set; }

    [JsonProperty("explicit")]
    public bool Explicit { get; set; }

    [JsonProperty("external_ids")]
    public ExternalIds ExternalIds { get; set; } = new();

    [JsonProperty("external_urls")]
    public ExternalUrls ExternalUrls { get; set; } = new();

    [JsonProperty("href")]
    public Uri Href
    {
        get =>
            new(
                _href
                    .ToString()
                    .Replace("api.", "open.")
                    .Replace("/v1", "")
                    .Replace("/tracks", "/track")
            );
        set => _href = value;
    }

    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("is_local")]
    public bool IsLocal { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("popularity")]
    public long Popularity { get; set; }

    [JsonProperty("preview_url")]
    public Uri? PreviewUrl { get; set; }

    [JsonProperty("track_number")]
    public long TrackNumber { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = null!;

    [JsonProperty("uri")]
    public Uri Uri { get; set; } = null!;
}

public class Album
{
    [JsonProperty("album_type")]
    public string AlbumType { get; set; } = null!;

    [JsonProperty("artists")]
    public Artist[] Artists { get; set; } = [];

    [JsonProperty("available_markets")]
    public string[] AvailableMarkets { get; set; } = [];

    [JsonProperty("external_urls")]
    public ExternalUrls ExternalUrls { get; set; } = new();

    [JsonProperty("href")]
    public Uri Href { get; set; } = null!;

    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("images")]
    public Image[] Images { get; set; } = [];

    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("release_date")]
    public DateTimeOffset ReleaseDate { get; set; } = new();

    [JsonProperty("release_date_precision")]
    public string ReleaseDatePrecision { get; set; } = null!;

    [JsonProperty("total_tracks")]
    public long TotalTracks { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = null!;

    [JsonProperty("uri")]
    public Uri Uri { get; set; } = null!;
}

public class Artist
{
    [JsonProperty("external_urls")]
    public ExternalUrls ExternalUrls { get; set; } = new();

    [JsonProperty("href")]
    public Uri Href { get; set; } = null!;

    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("type")]
    public string Type { get; set; } = null!;

    [JsonProperty("uri")]
    public string Uri { get; set; } = null!;
}

public class ExternalIds
{
    [JsonProperty("isrc")]
    public string Isrc { get; set; } = null!;
}
