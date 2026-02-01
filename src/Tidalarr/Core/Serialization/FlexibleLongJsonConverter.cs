using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tidalarr.Core.Serialization;

/// <summary>
/// Allows System.Text.Json to read string identifiers that occasionally appear as JSON strings
/// while keeping the domain model typed as long for convenience.
/// Handles both numeric and string representations of IDs from Tidal API.
/// </summary>
public sealed class FlexibleLongJsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => long.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                ? value
                : 0,
            JsonTokenType.Null => 0,
            JsonTokenType.None => throw new NotImplementedException(),
            JsonTokenType.StartObject => throw new NotImplementedException(),
            JsonTokenType.EndObject => throw new NotImplementedException(),
            JsonTokenType.StartArray => throw new NotImplementedException(),
            JsonTokenType.EndArray => throw new NotImplementedException(),
            JsonTokenType.PropertyName => throw new NotImplementedException(),
            JsonTokenType.Comment => throw new NotImplementedException(),
            JsonTokenType.True => throw new NotImplementedException(),
            JsonTokenType.False => throw new NotImplementedException(),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing a long value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
