using System.Text.Json.Serialization;

namespace ChzzkApi_CS;
public record ChzzkResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string? Message);

public record ChzzkResponse<T>(
    int Code,
    string? Message,
    [property: JsonPropertyName("content")] T? Content) : ChzzkResponse(Code, Message);

public record TokenContent(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    [property: JsonPropertyName("tokenType")] string TokenType,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope);
public record UserInfo(
    [property: JsonPropertyName("channelId")] string ChannelId,
    [property: JsonPropertyName("channelName")] string ChannelName);