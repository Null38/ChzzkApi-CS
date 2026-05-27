using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChzzkApi_CS.Session;

public record SessionListData(
    [property: JsonPropertyName("data")] List<SessionInfo> Data);

public record SessionInfo(
    [property: JsonPropertyName("sessionKey")] string SessionKey,
    [property: JsonPropertyName("connectedDate")] string ConnectedDate,
    [property: JsonPropertyName("disconnectedDate")] string DisconnectedDate,
    [property: JsonPropertyName("subscribedEvents")] List<EventData> SubscribedEvents);

public record EventData(
    [property: JsonPropertyName("eventType")] EventType EventType,
    [property: JsonPropertyName("channelId")] string ChannelId);

public record SessionCreateContent(
    [property: JsonPropertyName("url")] string Url);

public record SystemMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] JsonElement Data);

public record Badge(
    [property: JsonPropertyName("imageUrl")] string? ImageUrl);

public record ChatProfile(
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("badges")] List<Badge> Badges,
    [property: JsonPropertyName("verifiedMark")] bool VerifiedMark,
    [property: JsonPropertyName("userRoleCode")] UserRoleCode UserRoleCode);

public record ChatMessage(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("senderChannelId")] string SenderChannelId,
    [property: JsonPropertyName("chatChannelId")] string ChatChannelId,
    [property: JsonPropertyName("profile")] ChatProfile Profile,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("emojis")] Dictionary<string, string> Emojis,
    [property: JsonPropertyName("messageTime")] long MessageTime,
    [property: JsonPropertyName("eventSentAt")] string EventSentAt);

public record DonationMessage(
    [property: JsonPropertyName("donationType")] string DonationType,
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("donatorChannelId")] string DonatorChannelId,
    [property: JsonPropertyName("donatorNickname")] string DonatorNickname,
    [property: JsonPropertyName("payAmount")] string PayAmount,
    [property: JsonPropertyName("donationText")] string DonationText,
    [property: JsonPropertyName("emojis")] Dictionary<string, string> Emojis);

public record SubscriptionMessage(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("subscriberChannelId")] string SubscriberChannelId,
    [property: JsonPropertyName("subscriberNickname")] string SubscriberNickname,
    [property: JsonPropertyName("tierNo")] int TierNo,
    [property: JsonPropertyName("tierName")] string TierName,
    [property: JsonPropertyName("month")] int Month);

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
