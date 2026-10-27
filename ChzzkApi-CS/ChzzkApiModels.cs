using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChzzkApi;

public record ChzzkResponse(
    [property: JsonPropertyName("code")] HttpStatusCode Code,
    [property: JsonPropertyName("message")] string? Message)
{
    [JsonIgnore]
    public string Raw { get; private set; } = "";

    internal static T? Parse<T>(string raw)
        where T : ChzzkResponse
    {
        T? result = JsonSerializer.Deserialize<T>(raw);
        if (result is not null)
            result.Raw = raw;

        return result;
    }
}

public record ChzzkResponse<T>(
    HttpStatusCode Code,
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

public record ChatMessageContent(
    [property: JsonPropertyName("messageId")] string MessageId);

public record ChzzkPage<T>(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages,
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data);

public record ChzzkCursorPage<T>(
    [property: JsonPropertyName("page")] ChzzkCursor Page,
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data);

public record ChzzkCursor(
    [property: JsonPropertyName("next")] string? Next);
