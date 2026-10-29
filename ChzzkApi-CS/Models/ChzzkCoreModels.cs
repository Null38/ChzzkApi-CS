using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

public record ChzzkResponse(
    [property: JsonPropertyName("code")] HttpStatusCode Code,
    [property: JsonPropertyName("message")] string? Message)
{
    [JsonIgnore]
    public string Raw { get; private set; } = "";

    internal static T? Parse<T>(string raw)
        where T : ChzzkResponse
    {
        T? result = JsonSerializer.Deserialize<T>(raw, ChzzkJson.Options);
        if (result is not null)
            result.Raw = raw;

        return result;
    }
}

public record ChzzkResponse<T>(
    HttpStatusCode Code,
    string? Message,
    [property: JsonPropertyName("content")] T? Content) : ChzzkResponse(Code, Message);

public record ChzzkPage<T>(
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data);

public record ChzzkNumberedPage<T>(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("totalPages")] int TotalPages,
    IReadOnlyList<T> Data) : ChzzkPage<T>(Data);

public record ChzzkCursorPage<T>(
    [property: JsonPropertyName("page")] ChzzkCursor Page,
    IReadOnlyList<T> Data) : ChzzkPage<T>(Data);

public record ChzzkCursor(
    [property: JsonPropertyName("next")] string? Next);