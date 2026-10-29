using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

public record restrictedInfo(
    [property: JsonPropertyName("restrictedChannelId")] string RestrictedChannelId,
    [property: JsonPropertyName("restrictedChannelName")] string RestrictedChannelName,
    [property: JsonPropertyName("createdDate")] string CreatedDate,// to DateTime?
    [property: JsonPropertyName("releaseDate")] string? ReleaseDate);// to DateTime?