using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ChatAvailableConditionOption>))]
public enum ChatAvailableConditionOption
{
    [JsonStringEnumMemberName("NONE")] None,
    [JsonStringEnumMemberName("REAL_NAME")] RealName,
};

[JsonConverter(typeof(JsonStringEnumConverter<ChatAvailableGroupOption>))]
public enum ChatAvailableGroupOption
{
    [JsonStringEnumMemberName("ALL")] All,
    [JsonStringEnumMemberName("FOLLOWER")] Follower,
    [JsonStringEnumMemberName("MANAGER")] Manager,
    [JsonStringEnumMemberName("SUBSCRIBER")] Subscriber
};

public enum MinFollowerTimeOption
{
    Zero = 0,
    Min_5 = 5,
    Min_10 = 10,
    Min_30 = 30,
    Hour_1 = 60,
    Day_1 = 1440,
    Day_7 = 10080,
    Month_1 = 43200,
    Month_2 = 86400,
    Month_3 = 129600,
    Month_4 = 172800,
    Month_5 = 216000,
    Month_6 = 259200
}

public enum ChatSlowModeTimeOption
{
    None = 0,
    Sec_3 = 3,
    Sec_5 = 5,
    Sec_10 = 10,
    Sec30 = 30,
    Min_1 = 60,
    Min_2 = 120,
    Min_5 = 300
}

public record ChatMessageContent(
    [property: JsonPropertyName("messageId")] string MessageId);

public record ChatSettings(
    [property: JsonPropertyName("chatAvailableCondition")] ChatAvailableConditionOption ChatAvailableCondition,
    [property: JsonPropertyName("chatAvailableGroup")] ChatAvailableGroupOption ChatAvailableGroup,
    [property: JsonPropertyName("minFollowerMinute")] int MinFollowerMinute,// to TimeSpen?
    [property: JsonPropertyName("allowSubscriberInFollowerMode")] bool AllowSubscriberInFollowerMode,
    [property: JsonPropertyName("chatSlowModeSec")] int ChatSlowModeSec,// to TimeSpen?
    [property: JsonPropertyName("chatEmojiMode")] bool ChatEmojiMode);