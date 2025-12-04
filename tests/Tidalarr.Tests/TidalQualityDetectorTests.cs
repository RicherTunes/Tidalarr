using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Tests;

public class TidalQualityDetectorTests
{
    private readonly TidalQualityDetector _detector;

    public TidalQualityDetectorTests()
    {
        this._detector = new TidalQualityDetector();
    }

    [Theory]
    [InlineData("LOSSLESS", TidalQuality.Lossless)]
    [InlineData("HI_RES_LOSSLESS", TidalQuality.HiRes)]
    [InlineData("HIGH", TidalQuality.High)]
    [InlineData("LOW", TidalQuality.Low)]
    [InlineData("UNKNOWN_QUALITY", TidalQuality.High)] // Default fallback
    public void DetectQualityFromString_ValidInput_ReturnsCorrectQuality(string qualityString, TidalQuality expected)
    {
        // Act
        TidalQuality result = this._detector.DetectQualityFromString(qualityString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetectAvailableQualities_WithHiResTag_ReturnsAllQualities()
    {
        // Arrange
        string[] tags = ["HIRES_LOSSLESS"];

        // Act
        List<TidalQuality> qualities = this._detector.DetectAvailableQualities(tags);

        // Assert
        Assert.Contains(TidalQuality.HiRes, qualities);
        Assert.Contains(TidalQuality.Lossless, qualities);
        Assert.Contains(TidalQuality.High, qualities);
        Assert.Contains(TidalQuality.Low, qualities);
    }

    [Fact]
    public void DetectAvailableQualities_WithLosslessTag_ReturnsLosslessAndBelow()
    {
        // Arrange
        string[] tags = ["LOSSLESS"];

        // Act
        List<TidalQuality> qualities = this._detector.DetectAvailableQualities(tags);

        // Assert
        Assert.Contains(TidalQuality.Lossless, qualities);
        Assert.Contains(TidalQuality.High, qualities);
        Assert.Contains(TidalQuality.Low, qualities);
        Assert.DoesNotContain(TidalQuality.HiRes, qualities);
    }

    [Fact]
    public void DetectAvailableQualities_NoSpecialTags_ReturnsStandardQualities()
    {
        // Arrange
        string[] tags = ["SOME_OTHER_TAG"];

        // Act
        List<TidalQuality> qualities = this._detector.DetectAvailableQualities(tags);

        // Assert
        Assert.Contains(TidalQuality.High, qualities);
        Assert.Contains(TidalQuality.Low, qualities);
        Assert.DoesNotContain(TidalQuality.Lossless, qualities);
        Assert.DoesNotContain(TidalQuality.HiRes, qualities);
    }

    [Fact]
    public void SelectBestQuality_UserPrefersLossless_SelectsBestAvailable()
    {
        // Arrange
        TidalQuality[] availableQualities = [TidalQuality.High, TidalQuality.Lossless];
        TidalQuality userPreference = TidalQuality.HiRes; // User wants HiRes but not available

        // Act
        TidalQuality selected = this._detector.SelectBestQuality(availableQualities, userPreference);

        // Assert
        Assert.Equal(TidalQuality.Lossless, selected); // Best available
    }

    [Fact]
    public void SelectBestQuality_UserPrefersLow_RespectsUserChoice()
    {
        // Arrange
        TidalQuality[] availableQualities = [TidalQuality.High, TidalQuality.Lossless, TidalQuality.Low];
        TidalQuality userPreference = TidalQuality.Low;

        // Act
        TidalQuality selected = this._detector.SelectBestQuality(availableQualities, userPreference);

        // Assert
        Assert.Equal(TidalQuality.Low, selected); // Respect user choice
    }

    [Theory]
    [InlineData(new[] { "HIRES_LOSSLESS", "LOSSLESS" }, TidalQuality.HiRes)]
    [InlineData(new[] { "LOSSLESS" }, TidalQuality.Lossless)]
    [InlineData(new[] { "STANDARD" }, TidalQuality.High)]
    [InlineData(new string[0], TidalQuality.High)] // Empty tags
    public void DetectHighestAvailableQuality_FromTags_ReturnsExpected(string[] tags, TidalQuality expected)
    {
        // Act
        TidalQuality result = this._detector.DetectHighestAvailableQuality(tags);

        // Assert
        Assert.Equal(expected, result);
    }
}



