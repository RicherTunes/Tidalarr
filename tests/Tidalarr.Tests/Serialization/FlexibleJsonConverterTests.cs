using System.Text.Json;
using System.Text.Json.Serialization;
using Tidalarr.Core.Serialization;

namespace Tidalarr.Tests.Serialization;

/// <summary>
/// Tests for flexible JSON converters that handle type inconsistencies in Tidal API responses.
/// These converters allow the domain model to remain strongly typed while accommodating
/// API responses that inconsistently use strings vs numbers for the same fields.
/// </summary>
public class FlexibleJsonConverterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleLongJsonConverter(), new FlexibleStringJsonConverter() }
    };

    #region FlexibleLongJsonConverter - String to Long ID Parsing

    [Fact]
    public void FlexibleLongJsonConverter_StringId_DeserializesToLong()
    {
        // Arrange - API returns ID as string instead of number
        const string json = """{ "id": "123456789" }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123456789L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_NumericId_DeserializesToLong()
    {
        // Arrange - Normal number case
        const string json = """{ "id": 987654321 }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(987654321L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_NullId_ReturnsZero()
    {
        // Arrange - Null value should default to 0
        const string json = """{ "id": null }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_MaxLongValue_DeserializesCorrectly()
    {
        // Arrange - Edge case: maximum long value
        const string json = """{ "id": "9223372036854775807" }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(long.MaxValue, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_NegativeNumber_DeserializesCorrectly()
    {
        // Arrange - Negative number (unlikely for IDs but should work)
        const string json = """{ "id": -12345 }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(-12345L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_EmptyString_ReturnsZero()
    {
        // Arrange - Empty string should parse to 0
        const string json = """{ "id": "" }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_MalformedString_ReturnsZero()
    {
        // Arrange - Non-numeric string should return 0
        const string json = """{ "id": "not_a_number" }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_DecimalString_TruncatesToLong()
    {
        // Arrange - String with decimal should parse using Integer styles
        const string json = """{ "id": "123.456" }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        // TryParse with NumberStyles.Integer won't parse decimals, so returns 0
        Assert.Equal(0L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_WhitespaceString_ReturnsZero()
    {
        // Arrange - Whitespace-only string
        const string json = """{ "id": "   " }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_ZeroString_ParsesToZero()
    {
        // Arrange - String "0"
        const string json = """{ "id": "0" }""";

        // Act
        TestLongDto? result = JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0L, result.Id);
    }

    [Fact]
    public void FlexibleLongJsonConverter_SerializedLong_WritesAsNumber()
    {
        // Arrange - DTO with long value
        var dto = new TestLongDto { Id = 123456789L };

        // Act
        string json = JsonSerializer.Serialize(dto, JsonOptions);

        // Assert
        Assert.Contains("\"Id\":123456789", json);
    }

    [Fact]
    public void FlexibleLongJsonConverter_InvalidTokenType_ThrowsJsonException()
    {
        // Arrange - Boolean token is invalid for long conversion
        const string json = """{ "id": true }""";

        // Act & Assert
        JsonException exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<TestLongDto>(json, JsonOptions)
        );
        Assert.Contains("Unexpected token", exception.Message);
    }

    #endregion

    #region FlexibleStringJsonConverter - Number to String Value Parsing

    [Fact]
    public void FlexibleStringJsonConverter_NumberValue_DeserializesToString()
    {
        // Arrange - API returns number instead of string
        const string json = """{ "value": 12345 }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("12345", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_StringValue_DeserializesToString()
    {
        // Arrange - Normal string case
        const string json = """{ "value": "hello world" }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("hello world", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_NullValue_PreservesNullBehavior()
    {
        // Arrange - Null token is handled specially by System.Text.Json
        // The converter's Read method is not called for null values when using [JsonConverter] attribute
        const string json = """{ "value": null }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        // When null is in JSON, System.Text.Json sets property to null directly
        // without calling the converter's Read method
        Assert.Null(result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_LargeNumber_DeserializesToString()
    {
        // Arrange - Large number that exceeds int range
        const string json = """{ "value": 9223372036854775807 }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("9223372036854775807", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_NegativeNumber_DeserializesToString()
    {
        // Arrange - Negative number
        const string json = """{ "value": -9999 }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("-9999", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_FloatNumber_DeserializesToString()
    {
        // Arrange - Floating point number
        const string json = """{ "value": 123.456 }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123.456", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_ZeroNumber_DeserializesToString()
    {
        // Arrange - Number zero
        const string json = """{ "value": 0 }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("0", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_EmptyString_ReturnsEmptyString()
    {
        // Arrange - Empty string
        const string json = """{ "value": "" }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_SpecialCharacters_PreservesCorrectly()
    {
        // Arrange - String with special characters
        const string json = """{ "value": "hello\u0020world!@#$%" }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("hello world!@#$%", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_UnicodeCharacters_PreservesCorrectly()
    {
        // Arrange - String with Unicode characters
        const string json = """{ "value": "Hello 世界 🌊" }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello 世界 🌊", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_SerializedString_WritesAsString()
    {
        // Arrange - DTO with string value
        var dto = new TestStringDto { Value = "test value" };

        // Act
        string json = JsonSerializer.Serialize(dto, JsonOptions);

        // Assert
        Assert.Contains("\"Value\":\"test value\"", json);
    }

    [Fact]
    public void FlexibleStringJsonConverter_BooleanTrue_ConvertsToString()
    {
        // Arrange - Boolean true converts to "true" string
        const string json = """{ "value": true }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("true", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_BooleanFalse_ConvertsToString()
    {
        // Arrange - Boolean false converts to "false" string
        const string json = """{ "value": false }""";

        // Act
        TestStringDto? result = JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("false", result.Value);
    }

    [Fact]
    public void FlexibleStringJsonConverter_ArrayToken_ThrowsJsonException()
    {
        // Arrange - Array token cannot be converted to string
        const string json = """{ "value": [1, 2, 3] }""";

        // Act & Assert
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions)
        );
    }

    [Fact]
    public void FlexibleStringJsonConverter_ObjectToken_ThrowsJsonException()
    {
        // Arrange - Object token cannot be converted to string
        const string json = """{ "value": {} }""";

        // Act & Assert
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<TestStringDto>(json, JsonOptions)
        );
    }

    #endregion

    #region Test DTOs

    private record TestLongDto
    {
        [JsonConverter(typeof(FlexibleLongJsonConverter))]
        public required long Id { get; init; }
    }

    private record TestStringDto
    {
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public required string Value { get; init; } = string.Empty;
    }

    #endregion
}
