using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tidalarr.Core.Serialization;

/// <summary>
/// Allows System.Text.Json to read numeric identifiers that occasionally appear as JSON numbers
/// while keeping the domain model typed as string for convenience.
/// </summary>
public sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.TryGetInt64(out long value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Null => string.Empty,
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to string. Expected String, Number, Null, True, or False.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

