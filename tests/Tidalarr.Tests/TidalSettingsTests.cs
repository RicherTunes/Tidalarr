using Tidalarr.Integration;
using Xunit;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalSettingsTests
{
    [Fact]
    public void TidalSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var indexer = new TidalIndexerSettings();
        var download = new TidalDownloadSettings();

        // Assert
        Assert.Equal("US", indexer.TidalMarket);
        Assert.True(indexer.EnableCache);
        Assert.Equal(15, indexer.CacheDuration);
        Assert.True(download.IncludeMqa);
        Assert.Equal(TidalQuality.Lossless, download.PreferredQuality);
    }
    
    [Theory]
    [InlineData("", false, "Redirect URL is required for OAuth authentication")]
    [InlineData("not-a-url", false, "Redirect URL must be an absolute HTTP/HTTPS URL")]
    [InlineData("https://wrong-domain.com/auth", false, "Redirect URL must be under the tidal.com domain")]
    [InlineData("https://tidal.com/android/login/auth?code=test&state=test", true, "")]
    public void IsValid_VariousRedirectUrls_ValidatesCorrectly(string redirectUrl, bool expectedValid, string expectedError)
    {
        // Arrange
        var settings = new TidalIndexerSettings
        {
            RedirectUrl = redirectUrl,
            ConfigPath = "C:/temp"
        };
        
        // Act
        var isValid = settings.IsValid(out var errorMessage);
        
        // Assert
        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
            Assert.Contains(expectedError, errorMessage);
    }
    
    [Fact]
    public void TidalSettings_InheritsFromBaseStreamingSettings()
    {
        // Arrange & Act
        var indexer = new TidalIndexerSettings();
        var download = new TidalDownloadSettings();

        // Assert - Verify inheritance from shared library
        Assert.IsAssignableFrom<Lidarr.Plugin.Common.Base.BaseStreamingSettings>(indexer);
        Assert.IsAssignableFrom<Lidarr.Plugin.Common.Base.BaseStreamingSettings>(download);

        // Verify key fields
        Assert.True(indexer.EnableCache);
        Assert.True(indexer.CacheDuration > 0);
        Assert.Equal(TidalQuality.Lossless, download.PreferredQuality);
    }
    
    [Theory]
    [InlineData("US", true)]
    [InlineData("UK", true)]
    [InlineData("DE", true)]
    [InlineData("FR", true)]
    [InlineData("INVALID", false)]
    public void ValidateMarket_VariousMarkets_ValidatesCorrectly(string market, bool expectedValid)
    {
        // Arrange
        var settings = new TidalIndexerSettings
        {
            TidalMarket = market,
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=test",
            ConfigPath = "C:/temp"
        };
        
        // Act
        var isValid = settings.IsValid(out var errorMessage);
        
        // Assert
        Assert.Equal(expectedValid, isValid);
    }
}



