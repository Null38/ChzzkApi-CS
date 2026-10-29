using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

public record UserInfo(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("channelName")] string ChannelName,
    [property: JsonPropertyName("nickname")] string Nickname);