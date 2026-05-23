using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;

using Xunit;

namespace Tidalarr.Tests;

public sealed class TidalQualityDowngradeDetectorTests
{
    [Theory]
    [InlineData(TidalQuality.HiRes, TidalQuality.Lossless)]
    [InlineData(TidalQuality.HiRes, TidalQuality.High)]
    [InlineData(TidalQuality.HiRes, TidalQuality.Low)]
    [InlineData(TidalQuality.Lossless, TidalQuality.High)]
    [InlineData(TidalQuality.Lossless, TidalQuality.Low)]
    [InlineData(TidalQuality.High, TidalQuality.Low)]
    public void Detect_DowngradeFlagged_WhenDeliveredBelowRequested(TidalQuality requested, TidalQuality delivered)
    {
        var result = TidalQualityDowngradeDetector.Detect(requested, delivered);

        Assert.True(result.WasDowngraded);
        Assert.Equal(requested, result.Requested);
        Assert.Equal(delivered, result.Delivered);
        Assert.NotNull(result.Reason);
        Assert.Contains(delivered.ToString(), result.Reason);
        Assert.Contains(requested.ToString(), result.Reason);
    }

    [Theory]
    [InlineData(TidalQuality.HiRes, TidalQuality.HiRes)]
    [InlineData(TidalQuality.Lossless, TidalQuality.Lossless)]
    [InlineData(TidalQuality.High, TidalQuality.High)]
    [InlineData(TidalQuality.Low, TidalQuality.Low)]
    public void Detect_NoDowngrade_WhenDeliveredEqualsRequested(TidalQuality requested, TidalQuality delivered)
    {
        var result = TidalQualityDowngradeDetector.Detect(requested, delivered);

        Assert.False(result.WasDowngraded);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData(TidalQuality.Low, TidalQuality.Lossless)]
    [InlineData(TidalQuality.High, TidalQuality.HiRes)]
    public void Detect_NoDowngrade_WhenDeliveredHigherThanRequested(TidalQuality requested, TidalQuality delivered)
    {
        // Tidal upgrades are rare but harmless — not a downgrade.
        var result = TidalQualityDowngradeDetector.Detect(requested, delivered);

        Assert.False(result.WasDowngraded);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Reason_Points_Users_At_LiveTidalPlanPage()
    {
        // The remediation must point at an authoritative live source rather
        // than embedding specific tier names — Tidal restructures tiers and
        // any embedded name (HiFi/HiFi Plus/Free) will go stale.
        var result = TidalQualityDowngradeDetector.Detect(TidalQuality.HiRes, TidalQuality.Lossless);
        Assert.Contains("tidal.com/plans", result.Reason!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reason_Does_Not_Name_Obsolete_Tier_Names()
    {
        // Tidal Free was discontinued in 2024 and tier names have shifted.
        // Keep the message tier-name-free so it doesn't go stale on a rebrand.
        var result = TidalQualityDowngradeDetector.Detect(TidalQuality.Lossless, TidalQuality.High);
        Assert.DoesNotContain("HiFi Plus", result.Reason!, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tidal Free", result.Reason!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(99, 2)]
    [InlineData(2, 99)]
    [InlineData(-1, 3)]
    [InlineData(3, -1)]
    public void Detect_UndefinedEnumValue_DoesNotProduceWarning(int requested, int delivered)
    {
        // Legacy settings (e.g. a removed TidalQuality.Max=4 case) or any
        // future enum-value drift could leave an out-of-range int bound to
        // TidalQuality. The detector must not emit a gibberish reason like
        // "delivered '99' for a '2' request" — guard with Enum.IsDefined.
        var result = TidalQualityDowngradeDetector.Detect((TidalQuality)requested, (TidalQuality)delivered);

        Assert.False(result.WasDowngraded);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Reason_Mentions_HowToSilenceTheWarning()
    {
        // Users should know they can either upgrade Tidal OR lower their
        // Preferred Quality setting to silence the warning.
        var result = TidalQualityDowngradeDetector.Detect(TidalQuality.HiRes, TidalQuality.High);
        Assert.True(
            result.Reason!.Contains("Preferred Quality", System.StringComparison.OrdinalIgnoreCase) ||
            result.Reason!.Contains("upgrade", System.StringComparison.OrdinalIgnoreCase),
            $"Reason should hint at remediation; got: {result.Reason}");
    }
}
