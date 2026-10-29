using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<CategoryType>))]
public enum CategoryType
{
    [JsonStringEnumMemberName("GAME")] Game,
    [JsonStringEnumMemberName("SPORTS")] Sports,
    [JsonStringEnumMemberName("ETC")] Etc
};

public record CategoryInfo(
    [property: JsonPropertyName("categoryType")] CategoryType CategoryType,
    [property: JsonPropertyName("categoryId")] string CategoryId,
    [property: JsonPropertyName("categoryValue")] string CategoryValue,
    [property: JsonPropertyName("posterImageUrl")] string PosterImageUrl);