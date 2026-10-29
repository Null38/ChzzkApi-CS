using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

public enum SortOption
{
    Recent,
    Longer
}

public record ChannelInfo(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("channelName")] string ChannelName,
    [property: JsonPropertyName("channelImageUrl")] string ChannelImageUrl,
    [property: JsonPropertyName("followerCount")] int FollowerCount,
    [property: JsonPropertyName("verifiedMark")] bool VerifiedMark,
    [property: JsonPropertyName("verifiedMarkType")] string? VerifiedMarkType,
    [property: JsonPropertyName("officialChannel")] bool OfficialChannel);

public record ManagerInfo(
    [property: JsonPropertyName("managerChannelId")] string ManagerChannelId,
    [property: JsonPropertyName("managerChannelName")] string ManagerChannelName,
    [property: JsonPropertyName("userRole")] string UserRole,
    [property: JsonPropertyName("createdDate")] string CreatedDate);// to DateTime?

public record FollowerInfo(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("channelName")] string ChannelName,
    [property: JsonPropertyName("createdDate")] string CreatedDate);// to DateTime?

public record SubscriberInfo(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("channelName")] string ChannelName,
    [property: JsonPropertyName("month")] int Month,
    [property: JsonPropertyName("tierNo")] int TierNo,
    [property: JsonPropertyName("createdDate")] string CreatedDate);// to DateTime?