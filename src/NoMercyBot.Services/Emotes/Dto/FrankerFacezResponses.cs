using Newtonsoft.Json;

namespace NoMercyBot.Services.Emotes.Dto;

public class FrankerFacezResponse
{
    [JsonProperty("default_sets")]
    public int[] DefaultSets { get; set; } = [];

    [JsonProperty("sets")]
    public Dictionary<string, FrankerFacezSet> Sets { get; set; } = new();

    [JsonProperty("users")]
    public Dictionary<string, string[]> Users { get; set; } = new();
}

public class FrankerFacezSet
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("_type")]
    public int Type { get; set; }

    [JsonProperty("icon")]
    public object? Icon { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("css")]
    public object? Css { get; set; }

    [JsonProperty("emoticons")]
    public Emoticon[] Emoticons { get; set; } = [];
}

public class Emoticon
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("public")]
    public bool Public { get; set; }

    [JsonProperty("hidden")]
    public bool Hidden { get; set; }

    [JsonProperty("modifier")]
    public bool Modifier { get; set; }

    [JsonProperty("modifier_flags")]
    public int ModifierFlags { get; set; }

    [JsonProperty("offset")]
    public object? Offset { get; set; }

    [JsonProperty("margins")]
    public object? Margins { get; set; }

    [JsonProperty("css")]
    public object? Css { get; set; }

    [JsonProperty("owner")]
    public FrankerFacezOwner Owner { get; set; } = new();

    [JsonProperty("artist")]
    public object? Artist { get; set; }

    [JsonProperty("urls")]
    public Dictionary<string, Uri> Urls { get; set; } = new();

    [JsonProperty("status")]
    public int Status { get; set; }

    [JsonProperty("usage_count")]
    public int UsageCount { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("last_updated")]
    public DateTimeOffset LastUpdated { get; set; }
}

public class FrankerFacezOwner
{
    [JsonProperty("_id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("display_name")]
    public string DisplayName { get; set; } = string.Empty;
}
