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
    public void UnsupportedMarket_MessageListsSupportedValues()
    {
        var settings = new TidalLidarrIndexerSettings { TidalMarket = "ZZ" };
        var result = _validator.Validate(settings);

        Assert.False(result.IsValid);
        var error = result.Errors.Single(e => e.PropertyName == nameof(TidalLidarrIndexerSettings.TidalMarket));
        Assert.Contains("US", error.ErrorMessage);
        Assert.Contains("UK", error.ErrorMessage);
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
