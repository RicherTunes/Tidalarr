using System.Reflection;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Integration;

[Trait("Category", "Wave5")]
public class TidalarrValidationCodesTests
{
    private static IReadOnlyList<FieldInfo> GetCodeFields() =>
        typeof(TidalarrValidationCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToList();

    [Fact]
    public void AllCodes_AreUnique()
    {
        IReadOnlyList<FieldInfo> fields = GetCodeFields();
        string[] values = fields.Select(f => (string)f.GetRawConstantValue()!).ToArray();

        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void AllCodes_FollowTidPrefix()
    {
        // Convention: all validation codes start with "TID-"
        IReadOnlyList<FieldInfo> fields = GetCodeFields();

        foreach (FieldInfo field in fields)
        {
            string value = (string)field.GetRawConstantValue()!;
            Assert.StartsWith("TID-", value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AllCodes_AreUpperCaseWithDashes()
    {
        // Convention: codes use UPPER-CASE-WITH-DASHES only
        IReadOnlyList<FieldInfo> fields = GetCodeFields();

        foreach (FieldInfo field in fields)
        {
            string value = (string)field.GetRawConstantValue()!;
            Assert.Matches(@"^[A-Z][A-Z0-9\-]+$", value);
        }
    }

    [Fact]
    public void AllCodes_AreNonEmpty()
    {
        IReadOnlyList<FieldInfo> fields = GetCodeFields();
        Assert.NotEmpty(fields);

        foreach (FieldInfo field in fields)
        {
            string value = (string)field.GetRawConstantValue()!;
            Assert.False(string.IsNullOrWhiteSpace(value), $"Field {field.Name} must not be empty");
        }
    }

    [Fact]
    public void ExpectedCodes_ArePresent()
    {
        // Sanity-check that well-known codes are defined
        Assert.Equal("TID-CONFIG-REQUIRED", TidalarrValidationCodes.ConfigPathRequired);
        Assert.Equal("TID-CONFIG-PATH", TidalarrValidationCodes.ConfigPathInvalid);
        Assert.Equal("TID-REDIRECT-REQUIRED", TidalarrValidationCodes.RedirectRequired);
        Assert.Equal("TID-REDIRECT-URI", TidalarrValidationCodes.RedirectInvalidUri);
        Assert.Equal("TID-REDIRECT-DOMAIN", TidalarrValidationCodes.RedirectWrongDomain);
        Assert.Equal("TID-MARKET-UNSUPPORTED", TidalarrValidationCodes.MarketUnsupported);
        Assert.Equal("TID-EARLY-OUTOFRANGE", TidalarrValidationCodes.EarlyReleaseRange);
        Assert.Equal("TID-CACHE-RANGE", TidalarrValidationCodes.CacheDurationRange);
        Assert.Equal("TID-DOWNLOAD-REQUIRED", TidalarrValidationCodes.DownloadPathRequired);
        Assert.Equal("TID-DOWNLOAD-PATH", TidalarrValidationCodes.DownloadPathInvalid);
        Assert.Equal("TID-DOWNLOAD-DELAY", TidalarrValidationCodes.DownloadDelayRange);
        Assert.Equal("TID-DOWNLOAD-MAX-CONCURRENCY", TidalarrValidationCodes.MaxConcurrentTrackDownloadsRange);
        Assert.Equal("TID-DOWNLOAD-MAX-CHUNK-CONCURRENCY", TidalarrValidationCodes.MaxConcurrentChunkDownloadsRange);
        Assert.Equal("TID-QUALITY-INVALID", TidalarrValidationCodes.PreferredQualityInvalid);
    }
}
