using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChzzkApi.Models;

internal static class ChzzkJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new EmptyCollectionConverterFactory() }
    };

    class EmptyCollectionConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsGenericType
            && (typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            || typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                ? typeof(ListConverter<>) : typeof(DictionaryConverter<,>);

            return (JsonConverter)Activator.CreateInstance(converterType.MakeGenericType(typeToConvert.GetGenericArguments()))!;
        }
    }

    class ListConverter<T> : JsonConverter<IReadOnlyList<T>>
    {
        public override bool HandleNull => true;

        public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? [] : JsonSerializer.Deserialize<List<T>>(ref reader, options)!;

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value?.ToList(), options);
    }

    class DictionaryConverter<TKey, TValue> : JsonConverter<IReadOnlyDictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override bool HandleNull => true;

        public override IReadOnlyDictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? [] : JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options)!;

        public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<TKey, TValue> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value?.ToDictionary(), options);
    }
}
