using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
public class Channel : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(50)]
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [MaxLength(25)]
    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [MaxLength(450)]
    [JsonProperty("shoutout_template")]
    public string ShoutoutTemplate { get; set; } = AppDbConfig.DefaultShoutoutTemplate;

    [JsonProperty("last_shoutout")]
    public DateTime? LastShoutout { get; set; }

    [JsonProperty("shoutout_interval")]
    public int ShoutoutInterval { get; set; } = 10;

    [MaxLength(100)]
    [JsonProperty("username_pronunciation")]
    public string? UsernamePronunciation { get; set; }

    [ForeignKey(nameof(Id))]
    [JsonProperty("broadcaster")]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(Id))]
    [JsonProperty("info")]
    public virtual ChannelInfo Info { get; set; } = null!;

    public virtual ICollection<ChatPresence> UsersInChat { get; set; } = [];

    [JsonProperty("events")]
    public ICollection<ChannelEvent> Events { get; set; } = [];

    [JsonProperty("moderated_for")]
    public ICollection<ChannelModerator> ChannelModerators { get; set; } =
        new List<ChannelModerator>();
}

public sealed class SimpleChannel
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("enabled")]
    public bool Enabled { get; set; }

    [JsonProperty("broadcaster")]
    public SimpleUser User { get; set; }

    [JsonProperty("moderated_for")]
    public IEnumerable<SimpleChannelModerator> ChannelModerators { get; set; }

    public SimpleChannel(Channel channel)
    {
        Id = channel.Id;
        Name = channel.Name;
        Enabled = channel.Enabled;
        User = channel.User is not null ? new(channel.User) : null;
        ChannelModerators = channel.ChannelModerators.Select(m => new SimpleChannelModerator(
            m.User,
            m.Channel
        ));
    }
}
