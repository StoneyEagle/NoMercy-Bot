using Newtonsoft.Json;

namespace NoMercyBot.Services.Spotify.Dto;

public class SpotifyMeResponse
{
    [JsonProperty("country")]
    public string Country { get; set; }

    [JsonProperty("display_name")]
    public string DisplayName { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }

    [JsonProperty("explicit_content")]
    public ExplicitContent ExplicitContent { get; set; }

    [JsonProperty("external_urls")]
    public ExternalUrls ExternalUrls { get; set; }

    [JsonProperty("followers")]
    public Followers Followers { get; set; }

    [JsonProperty("href")]
    public Uri Href { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("images")]
    public Image[] Images { get; set; }

    [JsonProperty("product")]
    public string Product { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("uri")]
    public string Uri { get; set; }
}

public class ExplicitContent
{
    [JsonProperty("filter_enabled")]
    public bool FilterEnabled { get; set; }

    [JsonProperty("filter_locked")]
    public bool FilterLocked { get; set; }
}

public class ExternalUrls
{
    [JsonProperty("spotify")]
    public Uri Spotify { get; set; }
}

public class Followers
{
    [JsonProperty("href")]
    public object Href { get; set; }

    [JsonProperty("total")]
    public long Total { get; set; }
}

public class Image
{
    [JsonProperty("height")]
    public long Height { get; set; }

    [JsonProperty("url")]
    public Uri Url { get; set; }

    [JsonProperty("width")]
    public long Width { get; set; }
}
