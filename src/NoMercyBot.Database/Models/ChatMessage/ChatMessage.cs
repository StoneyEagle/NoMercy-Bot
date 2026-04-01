using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TwitchLib.EventSub.Core.EventArgs.Channel;

namespace NoMercyBot.Database.Models.ChatMessage;

[PrimaryKey(nameof(Id))]
public class ChatMessage : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    [MaxLength(255)]
    public string Id { get; set; } = null!;

    [JsonProperty("is_command")]
    public bool IsCommand { get; set; }

    [JsonProperty("is_cheer")]
    public bool IsCheer { get; set; }

    [JsonProperty("is_highlighted")]
    public bool IsHighlighted { get; set; }

    [JsonProperty("is_gigantified")]
    public bool IsGigantified { get; set; }

    [JsonProperty("is_decorated")]
    public bool IsDecorated { get; set; }

    [JsonProperty("bits_amount", NullValueHandling = NullValueHandling.Ignore)]
    public int? BitsAmount { get; set; }

    [JsonProperty("message_type")]
    public string MessageType { get; set; }

    [JsonProperty("decoration_style")]
    public string? DecorationStyle { get; set; }

    [JsonProperty("color_hex", NullValueHandling = NullValueHandling.Ignore)]
    public string ColorHex { get; set; } = null!;

    [JsonProperty("badges")]
    public List<ChatBadge> Badges { get; set; } = [];

    [MaxLength(50)]
    [ForeignKey(nameof(User))]
    [JsonProperty("user_id")]
    public string UserId { get; set; } = null!;

    [JsonProperty("user_name")]
    public string Username { get; set; } = null!;

    [JsonProperty("display_name")]
    public string DisplayName { get; set; } = null!;

    [JsonProperty("user_type")]
    public string UserType { get; set; } = null!;

    [JsonProperty("user")]
    public User User { get; set; } = null!;

    [JsonProperty("channel_id")]
    public string? BroadcasterId { get; set; }

    [JsonProperty("broadcaster")]
    public User Broadcaster { get; set; } = new();

    [JsonProperty("message")]
    public string Message { get; set; } = null!;

    [JsonProperty("fragments")]
    public List<ChatMessageFragment> Fragments { get; set; } = [];

    [JsonProperty("message_node", NullValueHandling = NullValueHandling.Ignore)]
    public MessageNode? MessageNode { get; set; }

    [JsonProperty("tmi_sent_ts")]
    public string TmiSentTs { get; set; } = null!;

    [JsonProperty("is_command_successful_reply", NullValueHandling = NullValueHandling.Ignore)]
    public string? SuccessfulReply { get; set; }

    [JsonProperty("reply_to_message_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReplyToMessageId { get; set; }

    [ForeignKey(nameof(ReplyToMessageId))]
    [JsonProperty("reply_to_message", NullValueHandling = NullValueHandling.Ignore)]
    public virtual ChatMessage? ReplyToMessage { get; set; }

    [JsonProperty("replies", NullValueHandling = NullValueHandling.Ignore)]
    public virtual ICollection<ChatMessage> Replies { get; set; } = [];

    [JsonProperty("deleted_at", NullValueHandling = NullValueHandling.Ignore)]
    public DateTime? DeletedAt { get; set; }

    [JsonProperty("stream_id")]
    public string? StreamId { get; set; }

    [JsonProperty("stream")]
    public Stream? Stream { get; set; }

    public ChatMessage() { }

    public ChatMessage(
        ChannelChatMessageArgs payloadEvent,
        Stream? currentStream,
        User user,
        User broadcaster
    )
    {
        User = user;
        UserId = payloadEvent.Payload.Event.ChatterUserId;
        Broadcaster = broadcaster;
        BroadcasterId = payloadEvent.Payload.Event.BroadcasterUserId;
        StreamId = currentStream?.Id;

        Id = payloadEvent.Payload.Event.MessageId;
        Username = payloadEvent.Payload.Event.ChatterUserLogin;
        DisplayName = payloadEvent.Payload.Event.ChatterUserName;
        Message = payloadEvent.Payload.Event.Message.Text;
        IsHighlighted = payloadEvent.Payload.Event.MessageType == "channel_points_highlighted";
        IsGigantified = payloadEvent.Payload.Event.MessageType == "power_ups_gigantified_emote";
        IsDecorated = payloadEvent.Payload.Event.MessageType == "power_ups_message_effect";
        IsCheer = payloadEvent.Payload.Event.Cheer != null;
        BitsAmount = payloadEvent.Payload.Event.Cheer?.Bits;
        MessageType = payloadEvent.Payload.Event.MessageType;
        DecorationStyle = payloadEvent.Payload.Event.ChannelPointsAnimationId;

        ColorHex = payloadEvent.Payload.Event.Color;
        Badges = GetBadges(payloadEvent);
        Fragments = MakeFragments(payloadEvent);

        // TODO: replace this!
        using AppDbContext dbContext = new();
        bool hasOriginMessage =
            payloadEvent.Payload.Event.Reply?.ParentMessageId is not null
            && dbContext.ChatMessages.Any(m =>
                m.Id == payloadEvent.Payload.Event.Reply.ParentMessageId
            );

        TmiSentTs = string.Empty;
        ReplyToMessageId = hasOriginMessage
            ? payloadEvent.Payload.Event.Reply?.ParentMessageId
            : null;
        UserType = GetUserType(payloadEvent);
    }

    private static List<ChatBadge> GetBadges(ChannelChatMessageArgs payloadEvent)
    {
        return payloadEvent
            .Payload.Event.Badges.Select(badge => new ChatBadge
            {
                SetId = badge.SetId,
                Id = badge.Id,
                Info = badge.Info,
            })
            .ToList();
    }

    private List<ChatMessageFragment> MakeFragments(ChannelChatMessageArgs payloadEvent)
    {
        List<ChatMessageFragment> fragments = payloadEvent
            .Payload.Event.Message.Fragments.Select(fragment => new ChatMessageFragment(fragment))
            .ToList();

        return fragments;
    }

    private string GetUserType(ChannelChatMessageArgs payloadEvent)
    {
        if (payloadEvent.Payload.Event.IsBroadcaster)
            return "Broadcaster";
        if (payloadEvent.Payload.Event.IsStaff)
            return "Staff";
        if (payloadEvent.Payload.Event.Badges.Any(b => b.Id == "lead_moderator"))
            return "LeadModerator";
        if (payloadEvent.Payload.Event.IsModerator)
            return "Moderator";
        if (payloadEvent.Payload.Event.IsVip)
            return "Vip";
        if (payloadEvent.Payload.Event.IsSubscriber)
            return "Subscriber";
        return "Viewer";
    }
}
