using Newtonsoft.Json;
using TwitchLib.Api.Helix.Models.ChannelPoints;

namespace NoMercyBot.Services.Twitch.Dto;

public class ChannelPointsCustomRewardsResponse
{
    [JsonProperty("data")] public List<ChannelPointsCustomRewardsResponseData> Data { get; set; }
}

public class ChannelPointsCustomRewardsResponseData
{
    [JsonProperty("broadcaster_name")] public string BroadcasterName { get; set; }
    [JsonProperty("broadcaster_login")] public string BroadcasterLogin { get; set; }
    [JsonProperty("broadcaster_id")] public string BroadcasterId { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("image")] public ChannelPointsCustomRewardsResponseDefaultImage Image { get; set; }
    [JsonProperty("background_color")] public string BackgroundColor { get; set; }
    [JsonProperty("is_enabled")] public bool IsEnabled { get; set; }
    [JsonProperty("cost")] public int Cost { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("prompt")] public string Prompt { get; set; }

    [JsonProperty("is_user_input_required")]
    public bool IsUserInputRequired { get; set; }

    [JsonProperty("max_per_stream_setting")]
    public ChannelPointsCustomRewardsResponseMaxPerStreamSetting MaxPerStreamSetting { get; set; }

    [JsonProperty("max_per_user_per_stream_setting")]
    public MaxPerUserPerStreamSetting MaxPerUserPerStreamSetting { get; set; }

    [JsonProperty("global_cooldown_setting")]
    public ChannelPointsCustomRewardsResponseGlobalCooldownSetting GlobalCooldownSetting { get; set; }

    [JsonProperty("is_paused")] public bool IsPaused { get; set; }
    [JsonProperty("is_in_stock")] public bool IsInStock { get; set; }
    [JsonProperty("default_image")] public ChannelPointsCustomRewardsResponseDefaultImage DefaultImage { get; set; }

    [JsonProperty("should_redemptions_skip_request_queue")]
    public bool ShouldRedemptionsSkipRequestQueue { get; set; }

    [JsonProperty("redemptions_redeemed_current_stream")]
    public object RedemptionsRedeemedCurrentStream { get; set; }

    [JsonProperty("cooldown_expires_at")] public object CooldownExpiresAt { get; set; }
}

public class ChannelPointsCustomRewardsResponseDefaultImage
{
    [JsonProperty("url_1x")] public Uri Url1X { get; set; }
    [JsonProperty("url_2x")] public Uri Url2X { get; set; }
    [JsonProperty("url_4x")] public Uri Url4X { get; set; }
}

public class ChannelPointsCustomRewardsResponseGlobalCooldownSetting
{
    [JsonProperty("is_enabled")] public bool IsEnabled { get; set; }

    [JsonProperty("global_cooldown_seconds")]
    public long GlobalCooldownSeconds { get; set; }
}

public class ChannelPointsCustomRewardsResponseMaxPerStreamSetting
{
    [JsonProperty("is_enabled")] public bool IsEnabled { get; set; }
    [JsonProperty("max_per_stream")] public long MaxPerStream { get; set; }
}

public class MaxPerUserPerStreamSetting
{
    [JsonProperty("is_enabled")] public bool IsEnabled { get; set; }

    [JsonProperty("max_per_user_per_stream")]
    public long MaxPerUserPerStream { get; set; }
}