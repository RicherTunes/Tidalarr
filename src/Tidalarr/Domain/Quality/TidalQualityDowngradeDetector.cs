using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Quality;

/// <summary>
/// Pure helper that compares the user's preferred <see cref="TidalQuality"/>
/// against the quality Tidal actually delivered for a track, and produces
/// a structured operator-facing reason string when a downgrade occurred.
/// </summary>
/// <remarks>
/// Without this check, the plugin silently downloads whatever Tidal returns —
/// leaving users with a "HiFi Plus required" subscription scenario unable
/// to tell why their HiRes-preferred downloads keep arriving as AAC 320.
/// </remarks>
public static class TidalQualityDowngradeDetector
{
    public readonly record struct Result(
        bool WasDowngraded,
        TidalQuality Requested,
        TidalQuality Delivered,
        string? Reason);

    /// <summary>
    /// Detect whether <paramref name="delivered"/> is below <paramref name="requested"/>.
    /// Returns a structured <see cref="Result"/> with an actionable
    /// remediation reason when so.
    /// </summary>
    public static Result Detect(TidalQuality requested, TidalQuality delivered)
    {
        // Guard against legacy/drifted settings storing an int outside the
        // enum's declared range — a missing case would otherwise produce a
        // gibberish reason like "delivered '99' for a '2' request".
        if (!System.Enum.IsDefined(typeof(TidalQuality), requested) ||
            !System.Enum.IsDefined(typeof(TidalQuality), delivered))
        {
            return new Result(false, requested, delivered, null);
        }

        if ((int)delivered < (int)requested)
        {
            return new Result(
                WasDowngraded: true,
                Requested: requested,
                Delivered: delivered,
                Reason: BuildReason(requested, delivered));
        }
        return new Result(false, requested, delivered, null);
    }

    private static string BuildReason(TidalQuality requested, TidalQuality delivered)
    {
        // Avoid naming specific Tidal tier names — Tidal restructured them in
        // 2024 and may again. Point users at the live plan comparison rather
        // than a claim that may go stale.
        return $"Tidal delivered '{delivered}' for a '{requested}' request. " +
               "Your Tidal account or region may not be entitled to the requested quality. " +
               $"Check tidal.com/plans for entitlement, or set Preferred Quality to '{delivered}' to stop seeing this warning.";
    }
}
