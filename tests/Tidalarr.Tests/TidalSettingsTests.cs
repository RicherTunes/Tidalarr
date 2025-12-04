using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalSettingsTests
{
    [Fact]
    public void TidalSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        TidalIndexerSettings indexer = new TidalIndexerSettings();
        TidalDownloadClientSettings download = new TidalDownloadClientSettings();

        // Assert
        Assert.Equal("US", indexer.TidalMarket);
        Assert.True(indexer.EnableCache);
        Assert.Equal(15, indexer.CacheDuration);
        Assert.True(download.IncludeMqa);
        Assert.Equal(TidalQuality.Lossless, download.PreferredQuality);
    }

    [Theory]
    [InlineData("", false, TidalarrValidationCodes.RedirectRequired)]
    [InlineData("not-a-url", false, TidalarrValidationCodes.RedirectInvalidUri)]
    [InlineData("https://wrong-domain.com/auth", false, TidalarrValidationCodes.RedirectWrongDomain)]
    [InlineData("https://tidal.com/android/login/auth?code=test&state=test", true, null)]
    public void ValidateRedirectUrl_ProducesExpectedDiagnostics(string redirectUrl, bool expectedValid, string? expectedErrorCode)
    {
        // Arrange
        TidalIndexerSettings settings = new TidalIndexerSettings
        {
            RedirectUrl = redirectUrl,
            ConfigPath = Path.GetTempPath()
        };

        // Act
        FluentValidation.Results.ValidationResult validation = settings.ValidateFluent();

        // Assert
        Assert.Equal(expectedValid, validation.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(expectedErrorCode, validation.Errors.Select(e => e.ErrorCode));
        }
    }

    [Fact]
    public void TidalSettings_InheritsFromBaseStreamingSettings()
    {
        // Arrange & Act
        TidalIndexerSettings indexer = new TidalIndexerSettings();
        TidalDownloadClientSettings download = new TidalDownloadClientSettings();

        // Assert - Verify inheritance from shared library
        _ = Assert.IsAssignableFrom<Lidarr.Plugin.Common.Base.BaseStreamingSettings>(indexer);
        _ = Assert.IsAssignableFrom<Lidarr.Plugin.Common.Base.BaseStreamingSettings>(download);

        // Verify key fields
        Assert.True(indexer.EnableCache);
        Assert.True(indexer.CacheDuration > 0);
        Assert.Equal(TidalQuality.Lossless, download.PreferredQuality);
    }

    [Theory]
    [InlineData("US", true, null)]
    [InlineData("UK", true, null)]
    [InlineData("DE", true, null)]
    [InlineData("FR", true, null)]
    [InlineData("INVALID", false, TidalarrValidationCodes.MarketUnsupported)]
    public void ValidateMarket_VariousMarkets_ValidatesCorrectly(string market, bool expectedValid, string? expectedErrorCode)
    {
        // Arrange
        TidalIndexerSettings settings = new TidalIndexerSettings
        {
            TidalMarket = market,
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=test",
            ConfigPath = Path.GetTempPath()
        };

        // Act
        FluentValidation.Results.ValidationResult validation = settings.ValidateFluent();

        // Assert
        Assert.Equal(expectedValid, validation.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(expectedErrorCode, validation.Errors.Select(e => e.ErrorCode));
        }
    }
}
