using System.Text.Json.Serialization;

namespace ChzzkApi_CS;
public interface IChzzkResponse
{
    int Code { get; }
    string? Message { get; }
}
public record ChzzkResponse<T>(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("content")] T? Content) : IChzzkResponse;

public record ChzzkResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string? Message) : IChzzkResponse;

public record TokenContent(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    [property: JsonPropertyName("tokenType")] string TokenType,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope);