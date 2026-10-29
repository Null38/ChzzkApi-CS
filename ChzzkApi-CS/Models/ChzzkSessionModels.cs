using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<SystemMessageType>))]
public enum SystemMessageType
{
    [JsonStringEnumMemberName("connected")] Connected,
    [JsonStringEnumMemberName("subscribed")] Subscribed,
    [JsonStringEnumMemberName("unsubscribed")] Unsubscribed,
    [JsonStringEnumMemberName("revoked")] Revoked
}

[JsonConverter(typeof(JsonStringEnumConverter<EventType>))]
public enum EventType
{
    [JsonStringEnumMemberName("CHAT")] Chat,
    [JsonStringEnumMemberName("DONATION")] Donation,
    [JsonStringEnumMemberName("SUBSCRIPTION")] Subscription
}

[JsonConverter(typeof(JsonStringEnumConverter<UserRoleCode>))]
public enum UserRoleCode
{
    [JsonStringEnumMemberName("streamer")] Streamer,
    [JsonStringEnumMemberName("common_user")] CommonUser,
    [JsonStringEnumMemberName("streaming_channel_manager")] StreamingChannelManager,
    [JsonStringEnumMemberName("streaming_chat_manager")] StreamingChatManager
}

public record SessionUrlContent(
    [property: JsonPropertyName("url")] string Url);

public record SessionInfo(
    [property: JsonPropertyName("sessionKey")] string SessionKey,
    [property: JsonPropertyName("connectedDate")] string ConnectedDate,// to DateTime?
    [property: JsonPropertyName("disconnectedDate")] string? DisconnectedDate,// to DateTime?
    [property: JsonPropertyName("subscribedEvents")] IReadOnlyList<EventData> SubscribedEvents);

public record EventData(
    [property: JsonPropertyName("eventType")] EventType EventType,
    [property: JsonPropertyName("channelId")] string ChannelId);

public record SystemMessage(
    [property: JsonPropertyName("type")] SystemMessageType Type,
    [property: JsonPropertyName("data")] SystemData Data);

public record SystemData(
    [property: JsonPropertyName("sessionKey")] string? SessionKey,
    [property: JsonPropertyName("eventType")] EventType? EventType,
    [property: JsonPropertyName("channelId")] string? ChannelId);

public record Badge(
    [property: JsonPropertyName("imageUrl")] string? ImageUrl);

public abstract record SessionMessage(
    [property: JsonPropertyName("channelId")] string ChannelId)
{
    [JsonIgnore]
    public string Raw { get; internal set; } = "";
}

public record ChatMessage(
    string ChannelId,
    [property: JsonPropertyName("chatChannelId")] string ChatChannelId,
    [property: JsonPropertyName("senderChannelId")] string SenderChannelId,
    [property: JsonPropertyName("profile")] ChatProfile Profile,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("emojis")] IReadOnlyDictionary<string, string> Emojis,
    [property: JsonPropertyName("messageTime")] long MessageTime,
    [property: JsonPropertyName("eventSentAt")] string EventSentAt) : SessionMessage(ChannelId);// to.. what? 나노초 9자리..

public record ChatProfile(
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("badges")] IReadOnlyList<Badge> Badges,
    [property: JsonPropertyName("verifiedMark")] bool VerifiedMark,
    [property: JsonPropertyName("userRoleCode")] UserRoleCode UserRoleCode);

public record DonationMessage(
    [property: JsonPropertyName("donationType")] string DonationType,
    string ChannelId,
    [property: JsonPropertyName("donatorChannelId")] string DonatorChannelId,
    [property: JsonPropertyName("donatorNickname")] string DonatorNickname,
    [property: JsonPropertyName("payAmount")] string PayAmount,
    [property: JsonPropertyName("donationText")] string DonationText,
    [property: JsonPropertyName("emojis")] IReadOnlyDictionary<string, string> Emojis) : SessionMessage(ChannelId);

public record SubscriptionMessage(
    string ChannelId,
    [property: JsonPropertyName("subscriberChannelId")] string SubscriberChannelId,
    [property: JsonPropertyName("subscriberNickname")] string SubscriberNickname,
    [property: JsonPropertyName("tierNo")] int TierNo,
    [property: JsonPropertyName("tierName")] string TierName,
    [property: JsonPropertyName("month")] int Month) : SessionMessage(ChannelId);