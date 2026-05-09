using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalQualityDetector - focuses on edge cases and all code paths.
/// Source: src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
/// </summary>
public class QualityDetectorCovTests
{
    private readonly TidalQualityDetector _detector = new();

    #region DetectQualityFromString

    [Fact]
    public void DetectQualityFromString_WithLow_ReturnsLow()
    {
        // Arrange & Act - Line 9-16: "LOW" => TidalQuality.Low
        // grep -n "LOW" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 11:            "LOW" => TidalQuality.Low,

        TidalQuality result = _detector.DetectQualityFromString("LOW");

        // Assert
        Assert.Equal(TidalQuality.Low, result);
    }

    [Fact]
    public void DetectQualityFromString_WithLowcase_ReturnsLow()
    {
        // Arrange & Act - Line 9: ToUpperInvariant handles lowercase
        // grep -n "ToUpperInvariant" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 9:        return qualityString?.ToUpperInvariant() switch

        TidalQuality result = _detector.DetectQualityFromString("low");

        // Assert
        Assert.Equal(TidalQuality.Low, result);
    }

    [Fact]
    public void DetectQualityFromString_WithHigh_ReturnsHigh()
    {
        // Arrange & Act - Line 12: "HIGH" => TidalQuality.High
        // grep -n "HIGH" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 12:            "HIGH" => TidalQuality.High,

        TidalQuality result = _detector.DetectQualityFromString("HIGH");

        // Assert
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void DetectQualityFromString_WithLossless_ReturnsLossless()
    {
        // Arrange & Act - Line 13: "LOSSLESS" => TidalQuality.Lossless
        // grep -n "LOSSLESS" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 13:            "LOSSLESS" => TidalQuality.Lossless,

        TidalQuality result = _detector.DetectQualityFromString("LOSSLESS");

        // Assert
        Assert.Equal(TidalQuality.Lossless, result);
    }

    [Fact]
    public void DetectQualityFromString_WithHiResLossless_ReturnsHiRes()
    {
        // Arrange & Act - Line 14: "HI_RES_LOSSLESS" => TidalQuality.HiRes
        // grep -n "HI_RES_LOSSLESS" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 14:            "HI_RES_LOSSLESS" => TidalQuality.HiRes,

        TidalQuality result = _detector.DetectQualityFromString("HI_RES_LOSSLESS");

        // Assert
        Assert.Equal(TidalQuality.HiRes, result);
    }

    [Fact]
    public void DetectQualityFromString_WithNull_ReturnsHighDefault()
    {
        // Arrange & Act - Line 9-16: null => TidalQuality.High (default fallback)
        // grep -n "Default fallback" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 15:            _ => TidalQuality.High // Default fallback

        TidalQuality result = _detector.DetectQualityFromString(null!);

        // Assert
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void DetectQualityFromString_WithUnknownString_ReturnsHighDefault()
    {
        // Arrange & Act - Line 15: _ => TidalQuality.High (default fallback)

        TidalQuality result = _detector.DetectQualityFromString("UNKNOWN_QUALITY");

        // Assert
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void DetectQualityFromString_WithEmptyString_ReturnsHighDefault()
    {
        // Arrange & Act - Line 15: empty string matches _ => TidalQuality.High

        TidalQuality result = _detector.DetectQualityFromString(string.Empty);

        // Assert
        Assert.Equal(TidalQuality.High, result);
    }

    #endregion

    #region DetectAvailableQualities

    [Fact]
    public void DetectAvailableQualities_WithEmptyTags_ReturnsLowAndHigh()
    {
        // Arrange & Act - Line 19-26: Always adds Low and High
        // grep -n "Always add basic qualities" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 23:            // Always add basic qualities

        List<TidalQuality> result = _detector.DetectAvailableQualities([]);

        // Assert - Should have Low and High only
        Assert.Equal(2, result.Count);
        Assert.Equal(TidalQuality.Low, result[0]);
        Assert.Equal(TidalQuality.High, result[1]);
    }

    [Fact]
    public void DetectAvailableQualities_WithLosslessTag_AddsLossless()
    {
        // Arrange & Act - Line 28-32: LOSSLESS tag adds Lossless
        // grep -n "LOSSLESS" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 29:        if (tags.Contains("LOSSLESS") || tags.Contains("HIRES_LOSSLESS"))

        List<TidalQuality> result = _detector.DetectAvailableQualities(["LOSSLESS"]);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(TidalQuality.Low, result);
        Assert.Contains(TidalQuality.High, result);
        Assert.Contains(TidalQuality.Lossless, result);
    }

    [Fact]
    public void DetectAvailableQualities_WithHiresLosslessTag_AddsLosslessAndHiRes()
    {
        // Arrange & Act - Line 34-38: HIRES_LOSSLESS tag adds HiRes
        // grep -n "HIRES_LOSSLESS" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 35:        if (tags.Contains("HIRES_LOSSLESS"))

        List<TidalQuality> result = _detector.DetectAvailableQualities(["HIRES_LOSSLESS"]);

        // Assert - Should have all four qualities
        Assert.Equal(4, result.Count);
        Assert.Equal(TidalQuality.Low, result[0]);
        Assert.Equal(TidalQuality.High, result[1]);
        Assert.Equal(TidalQuality.Lossless, result[2]);
        Assert.Equal(TidalQuality.HiRes, result[3]);
    }

    [Fact]
    public void DetectAvailableQualities_WithBothLosslessTags_ReturnsDistinctQualities()
    {
        // Arrange & Act - Line 40: Distinct() removes duplicates
        // grep -n "Distinct" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 40:        return [.. qualities.Distinct().OrderBy(q => (int)q)];

        List<TidalQuality> result = _detector.DetectAvailableQualities(["LOSSLESS", "HIRES_LOSSLESS"]);

        // Assert - Distinct should prevent duplicates
        Assert.Equal(4, result.Count);
        Assert.Equal(TidalQuality.Low, result[0]);
        Assert.Equal(TidalQuality.High, result[1]);
        Assert.Equal(TidalQuality.Lossless, result[2]);
        Assert.Equal(TidalQuality.HiRes, result[3]);
    }

    [Fact]
    public void DetectAvailableQualities_WithMultipleDuplicateTags_ReturnsDistinctQualities()
    {
        // Arrange & Act - Line 40: Distinct() handles multiple duplicates

        List<TidalQuality> result = _detector.DetectAvailableQualities(
        [
            "LOSSLESS",
            "LOSSLESS",
            "HIRES_LOSSLESS",
            "HIRES_LOSSLESS"
        ]);

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void DetectAvailableQualities_WithUnorderedTags_ReturnsOrderedQualities()
    {
        // Arrange & Act - Line 40: OrderBy sorts by int value

        List<TidalQuality> result = _detector.DetectAvailableQualities(
        [
            "HIRES_LOSSLESS",
            "LOSSLESS"
        ]);

        // Assert - Should be ordered by enum value
        Assert.Equal(TidalQuality.Low, result[0]);
        Assert.Equal(TidalQuality.High, result[1]);
        Assert.Equal(TidalQuality.Lossless, result[2]);
        Assert.Equal(TidalQuality.HiRes, result[3]);
    }

    #endregion

    #region SelectBestQuality

    [Fact]
    public void SelectBestQuality_WithEmptyAvailable_ReturnsHighFallback()
    {
        // Arrange & Act - Line 47-50: Empty returns High fallback
        // grep -n "Fallback" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 49:            return TidalQuality.High; // Fallback

        TidalQuality result = _detector.SelectBestQuality([], TidalQuality.HiRes);

        // Assert
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void SelectBestQuality_WithPreferenceAvailable_ReturnsPreference()
    {
        // Arrange & Act - Line 52-56: Returns userPreference if available
        // grep -n "user preference" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 53:        // If user preference is available, use it

        TidalQuality result = _detector.SelectBestQuality(
            [TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless],
            TidalQuality.Lossless);

        // Assert
        Assert.Equal(TidalQuality.Lossless, result);
    }

    [Fact]
    public void SelectBestQuality_WithPreferenceNotAvailable_ReturnsHighestSuitable()
    {
        // Arrange & Act - Line 58-63: Returns highest quality <= preference
        // grep -n "suitableQualities" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 59:        List<TidalQuality> suitableQualities = [.. available.Where(q => q <= userPreference)];

        TidalQuality result = _detector.SelectBestQuality(
            [TidalQuality.Low, TidalQuality.High],
            TidalQuality.HiRes);

        // Assert - HiRes not available, should return High (highest <= HiRes)
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void SelectBestQuality_WithAllQualitiesHigherThanPreference_ReturnsLowestAvailable()
    {
        // Arrange & Act - Line 65-67: Returns Min() when no suitable quality
        // grep -n "better than nothing" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 66:        // If no suitable quality, use the lowest available (better than nothing)

        TidalQuality result = _detector.SelectBestQuality(
            [TidalQuality.Lossless, TidalQuality.HiRes],
            TidalQuality.Low);

        // Assert - No quality <= Low, so returns lowest available (Lossless)
        Assert.Equal(TidalQuality.Lossless, result);
    }

    [Fact]
    public void SelectBestQuality_WithSingleAvailable_ReturnsThatQuality()
    {
        // Arrange & Act - Single element in available

        TidalQuality result = _detector.SelectBestQuality(
            [TidalQuality.Lossless],
            TidalQuality.HiRes);

        // Assert - Lossless is <= HiRes and is the only option
        Assert.Equal(TidalQuality.Lossless, result);
    }

    [Fact]
    public void SelectBestQuality_WithExactMatchLowPreference_ReturnsLow()
    {
        // Arrange & Act - Exact match case

        TidalQuality result = _detector.SelectBestQuality(
            [TidalQuality.Low, TidalQuality.High],
            TidalQuality.Low);

        // Assert
        Assert.Equal(TidalQuality.Low, result);
    }

    [Fact]
    public void SelectBestQuality_WithHiResPreferenceAndHiResAvailable_ReturnsHiRes()
    {
        // Arrange & Act - Highest quality available matches preference

        TidalQuality result = _detector.SelectBestQuality(
            [TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless, TidalQuality.HiRes],
            TidalQuality.HiRes);

        // Assert
        Assert.Equal(TidalQuality.HiRes, result);
    }

    #endregion

    #region DetectHighestAvailableQuality

    [Fact]
    public void DetectHighestAvailableQuality_WithEmptyTags_ReturnsHigh()
    {
        // Arrange & Act - Line 69-73: Empty tags => Max of [Low, High] or High fallback
        // grep -n "DetectHighestAvailableQuality" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 69:    public TidalQuality DetectHighestAvailableQuality(string[] tags)

        TidalQuality result = _detector.DetectHighestAvailableQuality([]);

        // Assert
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void DetectHighestAvailableQuality_WithHiresTag_ReturnsHiRes()
    {
        // Arrange & Act - Line 71-72: Max of available qualities

        TidalQuality result = _detector.DetectHighestAvailableQuality(["HIRES_LOSSLESS"]);

        // Assert
        Assert.Equal(TidalQuality.HiRes, result);
    }

    [Fact]
    public void DetectHighestAvailableQuality_WithLosslessTag_ReturnsLossless()
    {
        // Arrange & Act - Lossless is highest available

        TidalQuality result = _detector.DetectHighestAvailableQuality(["LOSSLESS"]);

        // Assert
        Assert.Equal(TidalQuality.Lossless, result);
    }

    #endregion

    #region IsQualityAvailable

    [Fact]
    public void IsQualityAvailable_WithQualityInTags_ReturnsTrue()
    {
        // Arrange & Act - Line 75-79: Contains check
        // grep -n "IsQualityAvailable" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 75:    public bool IsQualityAvailable(TidalQuality quality, string[] tags)

        bool result = _detector.IsQualityAvailable(TidalQuality.Lossless, ["LOSSLESS"]);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsQualityAvailable_WithQualityNotInTags_ReturnsFalse()
    {
        // Arrange & Act - HiRes not available without HIRES_LOSSLESS tag

        bool result = _detector.IsQualityAvailable(TidalQuality.HiRes, ["LOSSLESS"]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsQualityAvailable_WithEmptyTags_ReturnsFalseForHiRes()
    {
        // Arrange & Act - Only Low and High in empty tags

        bool result = _detector.IsQualityAvailable(TidalQuality.HiRes, []);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsQualityAvailable_WithEmptyTags_ReturnsTrueForHigh()
    {
        // Arrange & Act - High is always available

        bool result = _detector.IsQualityAvailable(TidalQuality.High, []);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsQualityAvailable_WithEmptyTags_ReturnsTrueForLow()
    {
        // Arrange & Act - Low is always available

        bool result = _detector.IsQualityAvailable(TidalQuality.Low, []);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region GetQualityDisplayName

    [Fact]
    public void GetQualityDisplayName_WithLow_ReturnsCorrectName()
    {
        // Arrange & Act - Line 83-91: Display name mapping
        // grep -n "GetQualityDisplayName" src/Tidalarr/Domain/Quality/TidalQualityDetector.cs
        // 81:    public string GetQualityDisplayName(TidalQuality quality)

        string result = _detector.GetQualityDisplayName(TidalQuality.Low);

        // Assert
        Assert.Equal("Low Quality (96 kbps AAC)", result);
    }

    [Fact]
    public void GetQualityDisplayName_WithHigh_ReturnsCorrectName()
    {
        // Arrange & Act

        string result = _detector.GetQualityDisplayName(TidalQuality.High);

        // Assert
        Assert.Equal("High Quality (320 kbps AAC)", result);
    }

    [Fact]
    public void GetQualityDisplayName_WithLossless_ReturnsCorrectName()
    {
        // Arrange & Act

        string result = _detector.GetQualityDisplayName(TidalQuality.Lossless);

        // Assert
        Assert.Equal("Lossless (FLAC 16-bit/44.1kHz)", result);
    }

    [Fact]
    public void GetQualityDisplayName_WithHiRes_ReturnsCorrectName()
    {
        // Arrange & Act

        string result = _detector.GetQualityDisplayName(TidalQuality.HiRes);

        // Assert
        Assert.Equal("Hi-Res (FLAC up to 24-bit/192kHz)", result);
    }

    #endregion

    #region Null-tags regression (wave 47)

    [Fact]
    public void DetectAvailableQualities_NullTags_TreatsAsEmpty_NoNRE()
    {
        // Tidal's API can return null mediaMetadata.tags. Pre-fix this NRE'd inside .Contains().
        var result = _detector.DetectAvailableQualities(null);

        Assert.NotNull(result);
        Assert.Contains(TidalQuality.Low, result);
        Assert.Contains(TidalQuality.High, result);
        Assert.DoesNotContain(TidalQuality.Lossless, result);
        Assert.DoesNotContain(TidalQuality.HiRes, result);
    }

    [Fact]
    public void DetectHighestAvailableQuality_NullTags_DefaultsToHigh_NoNRE()
    {
        var result = _detector.DetectHighestAvailableQuality(null);
        Assert.Equal(TidalQuality.High, result);
    }

    [Fact]
    public void IsQualityAvailable_NullTags_False_ForLossless_NoNRE()
    {
        Assert.False(_detector.IsQualityAvailable(TidalQuality.Lossless, null));
        Assert.False(_detector.IsQualityAvailable(TidalQuality.HiRes, null));
        // Low and High are baseline — always present.
        Assert.True(_detector.IsQualityAvailable(TidalQuality.Low, null));
        Assert.True(_detector.IsQualityAvailable(TidalQuality.High, null));
    }

    #endregion
}
