using System.Text.Json;
using Tidalarr.Core.Serialization;

namespace Tidalarr.Tests.Unit;

public class FlexibleLongJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new FlexibleLongJsonConverter() }
    };

    [Fact]
    public void Read_NumericToken_ReturnsValue()
    {
        long result = JsonSerializer.Deserialize<long>("12345678", Options);

        Assert.Equal(12345678L, result);
    }

    [Fact]
    public void Read_StringNumericToken_ReturnsValue()
    {
        long result = JsonSerializer.Deserialize<long>("\"12345678\"", Options);

        Assert.Equal(12345678L, result);
    }

    [Fact]
    public void Read_NullToken_ReturnsZero()
    {
        long result = JsonSerializer.Deserialize<long>("null", Options);

        Assert.Equal(0L, result);
    }

    [Fact]
    public void Read_NonNumericString_ReturnsZeroWithWarning()
    {
        // Non-numeric strings should return 0 (silent fallback for API resilience)
        // but ideally this would be logged. For now, verify it doesn't throw.
        long result = JsonSerializer.Deserialize<long>("\"abc\"", Options);

        Assert.Equal(0L, result);
    }

    [Fact]
    public void Read_EmptyString_ReturnsZero()
    {
        long result = JsonSerializer.Deserialize<long>("\"\"", Options);

        Assert.Equal(0L, result);
    }

    [Fact]
    public void Read_LargeNumber_ReturnsValue()
    {
        long result = JsonSerializer.Deserialize<long>("9223372036854775807", Options);

        Assert.Equal(long.MaxValue, result);
    }

    [Fact]
    public void Read_LargeNumberAsString_ReturnsValue()
    {
        long result = JsonSerializer.Deserialize<long>("\"9223372036854775807\"", Options);

        Assert.Equal(long.MaxValue, result);
    }

    [Fact]
    public void Read_NegativeNumber_ReturnsValue()
    {
        long result = JsonSerializer.Deserialize<long>("-42", Options);

        Assert.Equal(-42L, result);
    }

    [Fact]
    public void Write_WritesNumberValue()
    {
        string json = JsonSerializer.Serialize(12345678L, Options);

        Assert.Equal("12345678", json);
    }

    [Fact]
    public void Read_BooleanToken_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<long>("true", Options));
    }
}
