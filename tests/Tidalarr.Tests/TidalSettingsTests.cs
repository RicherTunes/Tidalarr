using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

public class TidalSettingsTests
{
    [Fact]
    public void TidalSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new TidalSettings();
        
        // Assert
        Assert.Equal("US", settings.TidalMarket);
        Assert.True(settings.IncludeMqa);
        Assert.Equal("Lossless", settings.PreferredQuality);
        Assert.True(settings.EnableCache);
        Assert.Equal(15, settings.CacheDuration);
    }
    
    [Theory]
    [InlineData("", false, "Redirect URL is required")]
    [InlineData("not-a-url", false, "Invalid redirect URL format")]
    [InlineData("https://wrong-domain.com/auth", false, "Invalid callback domain")]
    [InlineData("https://tidal.com/android/login/auth?code=test&state=test", true, "")]
    public void IsValid_VariousRedirectUrls_ValidatesCorrectly(string redirectUrl, bool expectedValid, string expectedError)
    {
        // Arrange
        var settings = new TidalSettings
        {
            RedirectUrl = redirectUrl
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
        var settings = new TidalSettings();
        
        // Assert - Verify inheritance from shared library
        Assert.IsAssignableFrom<Lidarr.Plugin.Common.Base.BaseStreamingSettings>(settings);
        
        // Verify shared library fields are available
        Assert.NotNull(settings.PreferredQuality);
        Assert.True(settings.EnableCache);
        Assert.True(settings.CacheDuration > 0);
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
        var settings = new TidalSettings
        {
            TidalMarket = market,
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=test"
        };
        
        // Act
        var isValid = settings.IsValid(out var errorMessage);
        
        // Assert
        Assert.Equal(expectedValid, isValid);
    }
}
