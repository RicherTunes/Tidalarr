using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalQualityDetector.
/// Tests uncovered paths: null input, edge cases, IsQualityAvailable, GetQualityDisplayName.
/// </summary>
public class TidalQualityDetectorCovTests
{
    private readonly TidalQualityDetector _detector = new();

    #region DetectQualityFromString - Null Input

    [Fact]
    public void DetectQualityFromString_NullInput_ReturnsHighDefault()
    {
        // Act - null input triggers default case in switch expression
        TidalQuality result = _detector.DetectQualityFromString(null!);

        // Assert - line 14: default fallback returns High
        Assert.Equal(TidalQuality.High, result);
    }

    #endregion

    #region DetectAvailableQualities - Both Lossless and HiRes Tags

    [Fact]
    public void DetectAvailableQualities_WithBothLosslessAndHiResTags_ReturnsDistinctOrdered()
    {
        // Arrange - both tags present to test distinct/ordering logic (lines 27-28)
        string[] tags = ["LOSSLESS", "HIRES_LOSSLESS"];

        // Act
        List<TidalQuality> qualities = _detector.DetectAvailableQualities(tags);

        // Assert - should contain all 4 qualities, ordered by enum value
        Assert.Equal(4, qualities.Count);
        Assert.Equal([TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless, TidalQuality.HiRes], qualities);
    }

    #endregion

    #region SelectBestQuality - Empty Available Qualities

    [Fact]
    public void SelectBestQuality_EmptyAvailable_ReturnsHighFallback()
    {
        // Arrange - empty list triggers line 51 fallback
        TidalQuality[] availableQualities = [];

        // Act
        TidalQuality selected = _detector.SelectBestQuality(availableQualities, TidalQuality.HiRes);

        // Assert - line 52: returns High fallback when no qualities available
        Assert.Equal(TidalQuality.High, selected);
    }

    #endregion

    #region SelectBestQuality - No Suitable Qualities Below Preference

    [Fact]
    public void SelectBestQuality_NoSuitableBelowPreference_ReturnsLowestAvailable()
    {
        // Arrange - user wants Low but only High/Lossless available (no suitable below preference)
        // This triggers line 68: return available.Min() when no suitable qualities
        TidalQuality[] availableQualities = [TidalQuality.High, TidalQuality.Lossless];
        TidalQuality userPreference = TidalQuality.Low;

        // Act
        TidalQuality selected = _detector.SelectBestQuality(availableQualities, userPreference);

        // Assert - line 68: returns lowest available when no suitable quality found
        Assert.Equal(TidalQuality.High, selected);
    }

    #endregion

    #region IsQualityAvailable - Full Coverage

    [Fact]
    public void IsQualityAvailable_QualityPresent_ReturnsTrue()
    {
        // Arrange
        string[] tags = ["HIRES_LOSSLESS"];
        TidalQuality quality = TidalQuality.HiRes;

        // Act - line 81: checks if quality is in detected available qualities
        bool result = _detector.IsQualityAvailable(quality, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsQualityAvailable_QualityNotPresent_ReturnsFalse()
    {
        // Arrange
        string[] tags = ["LOSSLESS"]; // No HiRes tag
        TidalQuality quality = TidalQuality.HiRes;

        // Act - line 81: HiRes not available without HIRES_LOSSLESS tag
        bool result = _detector.IsQualityAvailable(quality, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsQualityAvailable_WithEmptyTags_ReturnsBasicQualitiesOnly()
    {
        // Arrange
        string[] tags = [];
        TidalQuality highQuality = TidalQuality.High;
        TidalQuality losslessQuality = TidalQuality.Lossless;

        // Act
        bool highAvailable = _detector.IsQualityAvailable(highQuality, tags);
        bool losslessAvailable = _detector.IsQualityAvailable(losslessQuality, tags);

        // Assert - High always available, Lossless requires tag
        Assert.True(highAvailable);
        Assert.False(losslessAvailable);
    }

    #endregion

    #region GetQualityDisplayName - Full Coverage

    [Fact]
    public void GetQualityDisplayName_ForLow_ReturnsCorrectFormat()
    {
        // Act - line 89: Low case
        string displayName = _detector.GetQualityDisplayName(TidalQuality.Low);

        // Assert
        Assert.Equal("Low Quality (96 kbps AAC)", displayName);
    }

    [Fact]
    public void GetQualityDisplayName_ForHigh_ReturnsCorrectFormat()
    {
        // Act - line 90: High case
        string displayName = _detector.GetQualityDisplayName(TidalQuality.High);

        // Assert
        Assert.Equal("High Quality (320 kbps AAC)", displayName);
    }

    [Fact]
    public void GetQualityDisplayName_ForLossless_ReturnsCorrectFormat()
    {
        // Act - line 91: Lossless case
        string displayName = _detector.GetQualityDisplayName(TidalQuality.Lossless);

        // Assert
        Assert.Equal("Lossless (FLAC 16-bit/44.1kHz)", displayName);
    }

    [Fact]
    public void GetQualityDisplayName_ForHiRes_ReturnsCorrectFormat()
    {
        // Act - line 92: HiRes case
        string displayName = _detector.GetQualityDisplayName(TidalQuality.HiRes);

        // Assert
        Assert.Equal("Hi-Res (FLAC up to 24-bit/192kHz)", displayName);
    }

    [Fact]
    public void GetQualityDisplayName_ForInvalidValue_ReturnsUnknown()
    {
        // Arrange - use invalid enum value to trigger default case (line 93)
        TidalQuality invalidQuality = (TidalQuality)999;

        // Act - line 93: default case returns "Unknown Quality"
        string displayName = _detector.GetQualityDisplayName(invalidQuality);

        // Assert
        Assert.Equal("Unknown Quality", displayName);
    }

    #endregion
}
