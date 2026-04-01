using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(ChannelId), nameof(UserId))]
[Index(nameof(UserId))]
[Index(nameof(ChannelId))]
public class ChannelModerator : Timestamps
{
    [JsonProperty("channel_id")]
    public string ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;

    [JsonProperty("user_id")]
    public string UserId { get; set; }
    public User User { get; set; } = null!;

    public ChannelModerator()
    {
        //
    }

    public ChannelModerator(string channelId, string userId)
    {
        ChannelId = channelId;
        UserId = userId;
    }
}

public class SimpleChannelModerator
{
    [JsonProperty("channel_id")]
    public string ChannelId { get; set; }

    [JsonProperty("user_id")]
    public string UserId { get; set; }

    [JsonProperty("user_name")]
    public string Username { get; set; }

    [JsonProperty("display_name")]
    public string DisplayName { get; set; }

    public SimpleChannelModerator(User user, Channel channel)
    {
        ChannelId = channel.Id;
        UserId = user.Id;
        Username = user.Username;
        DisplayName = user.DisplayName;
    }
}
