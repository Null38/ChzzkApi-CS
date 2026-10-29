using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

public record LiveInfo(
    [property: JsonPropertyName("liveId")] int LiveId,
    [property: JsonPropertyName("liveTitle")] string LiveTitle,
    [property: JsonPropertyName("liveThumbnailImageUrl")] string LiveThumbnailImageUrl,
    [property: JsonPropertyName("concurrentUserCount")] int ConcurrentUserCount,
    [property: JsonPropertyName("openDate")] string OpenDate,// to DateTime?
    [property: JsonPropertyName("adult")] bool Adult,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("categoryType")] CategoryType CategoryType,
    [property: JsonPropertyName("liveCategory")] string LiveCategory,
    [property: JsonPropertyName("liveCategoryValue")] string LiveCategoryValue,
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("channelName")] string ChannelName,
    [property: JsonPropertyName("channelImageUrl")] string ChannelImageUrl);

public record StreamKeyContent(
    [property: JsonPropertyName("streamKey")] string StreamKey);


public record StreamSettings(
    [property: JsonPropertyName("defaultLiveTitle")] string DefaultLiveTitle,
    [property: JsonPropertyName("category")] CategoryInfo Category,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags);