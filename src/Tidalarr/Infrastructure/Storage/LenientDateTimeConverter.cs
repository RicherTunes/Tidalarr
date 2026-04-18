using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tidalarr.Infrastructure.Storage;

/// <summary>
/// Allows reading DateTime values from legacy non-ISO formats previously written by older token stores.
/// Always writes timestamps in ISO 8601 (UTC) round-trip format.
/// </summary>
public sealed class LenientDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly string[] LegacyFormats =
    [
        "MM/dd/yyyy HH:mm:ss",
        "M/d/yyyy H:mm:ss",
        "MM/dd/yyyy H:mm:ss",
        "M/d/yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss",
        "d/M/yyyy H:mm:ss",
        "dd/MM/yyyy H:mm:ss",
        "d/M/yyyy HH:mm:ss"
    ];

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return default;
            }

            if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                    out DateTime parsedIso))
            {
                return NormalizeToUtc(parsedIso);
            }

            foreach (string format in LegacyFormats)
            {
                if (DateTime.TryParseExact(
                        value,
                        format,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out DateTime parsedLegacy))
                {
                    return NormalizeToUtc(parsedLegacy);
                }
            }

            if (DateTime.TryParse(
                    value,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                    out DateTime parsedCurrentCulture))
            {
                return NormalizeToUtc(parsedCurrentCulture);
            }

            throw new JsonException($"Invalid DateTime value: '{value}'");
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out long epoch))
            {
                // Heuristic: treat large values as milliseconds.
                DateTimeOffset dto = epoch > 10_000_000_000L
                    ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                    : DateTimeOffset.FromUnixTimeSeconds(epoch);
                return dto.UtcDateTime;
            }
        }

        throw new JsonException($"Unexpected token parsing DateTime. TokenType={reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        DateTime utc = NormalizeToUtc(value);
        writer.WriteStringValue(utc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}

