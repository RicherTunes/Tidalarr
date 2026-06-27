using System.Linq;
using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Wave 68 TDD: validator messages must guide the user toward a fix.
/// </summary>
public sealed class TidalLidarrIndexerSettingsValidatorTests
{
    private readonly TidalLidarrIndexerSettingsValidator _validator = new();

    [Fact]
    public void EmptyConfigPath_MessageHintsAtDefaultLocation()
    {
        var settings = new TidalLidarrIndexerSettings { ConfigPath = string.Empty };
        var result = _validator.Validate(settings);

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == nameof(TidalLidarrIndexerSettings.ConfigPath));
        Assert.True(
            error.ErrorMessage.Contains("/config/Tidalarr") ||
            error.ErrorMessage.Contains("AppData") ||
            error.ErrorMessage.Contains("default"),
            $"ConfigPath error should hint at default location: {error.ErrorMessage}");
    }

    [Fact]
    public void InvalidFormatMarket_ProducesActionableError()
    {
        // The validator now checks FORMAT (2-letter ISO) instead of a fixed allowlist — Tidal rejects
        // unknown codes at runtime, and the old allowlist both rejected the correct "GB" and accepted
        // the wrong "UK". A wrong-length value still fails with a helpful message.
        var settings = new TidalLidarrIndexerSettings { ConfigPath = "/config/Tidalarr", TidalMarket = "USA" };
        var result = _validator.Validate(settings);

        var error = result.Errors.Single(e => e.PropertyName == nameof(TidalLidarrIndexerSettings.TidalMarket));
        Assert.Contains("2-letter", error.ErrorMessage);
    }

    [Theory]
    [InlineData("GB")] // the correct ISO code the old allowlist WRONGLY rejected
    [InlineData("UK")] // tolerated (normalized to GB at the API boundary)
    [InlineData("US")]
    [InlineData("JP")]
    public void ValidTwoLetterMarket_PassesValidation(string market)
    {
        var settings = new TidalLidarrIndexerSettings { ConfigPath = "/config/Tidalarr", TidalMarket = market };
        var result = _validator.Validate(settings);

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(TidalLidarrIndexerSettings.TidalMarket));
    }

    [Fact]
    public void RedirectUrl_NotRequiredOnFirstSetup()
    {
        var settings = new TidalLidarrIndexerSettings
        {
            ConfigPath = "/tmp/test",
            RedirectUrl = string.Empty,
            TidalMarket = "US",
        };
        var result = _validator.Validate(settings);

        Assert.DoesNotContain(result.Errors, e => e.PropertyName == nameof(TidalLidarrIndexerSettings.RedirectUrl));
    }

    [Fact]
    public void RedirectUrl_NonHttpScheme_Rejected()
    {
        var settings = new TidalLidarrIndexerSettings
        {
            ConfigPath = "/tmp/test",
            RedirectUrl = "javascript:alert(1)",
            TidalMarket = "US",
        };
        var result = _validator.Validate(settings);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(TidalLidarrIndexerSettings.RedirectUrl));
    }
}
