using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tidalarr.Core.Json;

public sealed class JsonStringOrNumberConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var number)
                ? number.ToString(CultureInfo.InvariantCulture)
                : reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token type {reader.TokenType} when parsing a string/number value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

