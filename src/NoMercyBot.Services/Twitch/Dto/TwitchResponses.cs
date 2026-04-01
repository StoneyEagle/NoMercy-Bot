using Newtonsoft.Json;

namespace NoMercyBot.Services.Twitch.Dto;

public class TwitchAuthResponse
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public long ExpiresIn { get; set; }

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonProperty("scope")]
    public string[] Scope { get; set; }

    [JsonProperty("token_type")]
    public string TokenType { get; set; }
}

public class TwitchErrorResponse
{
    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("status")]
    public int? Status { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }
}

public class DeviceCodeRequest
{
    [JsonProperty("device_code")]
    public string DeviceCode { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = null!;
}

public class DeviceCodeResponse
{
    [JsonProperty("device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonProperty("user_code")]
    public string UserCode { get; set; } = string.Empty;

    [JsonProperty("verification_uri")]
    public string VerificationUri { get; set; } = string.Empty;

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty("interval")]
    public int Interval { get; set; }
}

public class ValidatedTwitchAuthResponse
{
    [JsonProperty("client_id")]
    public string ClientId { get; set; } = null!;

    [JsonProperty("login")]
    public string Login { get; set; } = null!;

    [JsonProperty("scopes")]
    public string[] Scopes { get; set; } = null!;

    [JsonProperty("user_id")]
    public string UserId { get; set; } = null!;

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
}

public class UserInfoResponse
{
    [JsonProperty("data")]
    public List<UserInfo> Data { get; set; } = [];
}

public class UserInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("login")]
    public string Login { get; set; } = string.Empty;

    [JsonProperty("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("broadcaster_type")]
    public string BroadcasterType { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("profile_image_url")]
    public string ProfileImageUrl { get; set; } = string.Empty;

    [JsonProperty("offline_image_url")]
    public string OfflineImageUrl { get; set; } = string.Empty;

    [JsonProperty("view_count")]
    public int ViewCount { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class ChannelInfoResponse
{
    [JsonProperty("data")]
    public List<ChannelInfoDto> Data { get; set; } = [];
}

public class ChannelInfoDto
{
    [JsonProperty("broadcaster_id")]
    public string BroadcasterId { get; set; } = string.Empty;

    [JsonProperty("broadcaster_login")]
    public string BroadcasterLogin { get; set; } = string.Empty;

    [JsonProperty("broadcaster_name")]
    public string BroadcasterName { get; set; } = string.Empty;

    [JsonProperty("broadcaster_language")]
    public string Language { get; set; } = string.Empty;

    [JsonProperty("game_id")]
    public string GameId { get; set; } = string.Empty;

    [JsonProperty("game_name")]
    public string GameName { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("delay")]
    public int Delay { get; set; }

    [JsonProperty("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonProperty("content_classification_labels")]
    public List<string> ContentLabels { get; set; } = [];

    [JsonProperty("is_branded_content")]
    public bool IsBrandedContent { get; set; }
}

public class StreamInfoResponse
{
    [JsonProperty("data")]
    public List<StreamInfo> Data { get; set; } = [];

    [JsonProperty("pagination")]
    public Dictionary<string, string> Pagination { get; set; } = new();
}

public class StreamInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("user_login")]
    public string UserLogin { get; set; } = string.Empty;

    [JsonProperty("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonProperty("game_id")]
    public string GameId { get; set; } = string.Empty;

    [JsonProperty("game_name")]
    public string GameName { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonProperty("viewer_count")]
    public int ViewerCount { get; set; }

    [JsonProperty("started_at")]
    public DateTime StartedAt { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; } = string.Empty;

    [JsonProperty("thumbnail_url")]
    public string ThumbnailUrl { get; set; } = string.Empty;

    [JsonProperty("tag_ids")]
    public List<string> TagIds { get; set; } = [];

    [JsonProperty("is_mature")]
    public bool IsMature { get; set; }
}

public class TokenResponse
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
}

public class ChannelFollowersResponse
{
    [JsonProperty("total")]
    public long Total { get; set; }

    [JsonProperty("data")]
    public ChannelFollowersResponseData[] Data { get; set; }

    [JsonProperty("pagination")]
    public Pagination Pagination { get; set; } = new();
}

public class ChannelFollowersResponseData
{
    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonProperty("user_login")]
    public string UserLogin { get; set; } = string.Empty;

    [JsonProperty("followed_at")]
    public DateTimeOffset FollowedAt { get; set; }
}

public class Pagination
{
    [JsonProperty("cursor")]
    public string Cursor { get; set; } = string.Empty;
}
