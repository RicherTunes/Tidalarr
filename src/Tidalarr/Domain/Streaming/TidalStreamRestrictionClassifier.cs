using Tidalarr.Core.Exceptions;

namespace Tidalarr.Domain.Streaming;

/// <summary>
/// Maps a failed Tidal <c>playbackinfopostpaywall</c> response to a
/// <see cref="TidalStreamUnavailableReason"/>. Classification correctness is the whole game for
/// terminal-release suppression: only an unambiguous "the asset is gone from the catalog" signal is
/// treated as PERMANENT; everything else — auth, region/tier, not-ready, rate-limit, server, network,
/// and anything unrecognized — defaults to TRANSIENT so a recoverable album is never permanently hidden.
/// </summary>
/// <remarks>
/// <para><b>Why 404 → permanent.</b> Tidal returns per-track availability failures with distinct signals:
/// region / subscription gating surfaces as HTTP 401/403 with a sub-status, "still processing" as 401
/// sub-status 4005, and rate-limit/outage as 429/5xx. A 404 from the track's own playback-info endpoint —
/// for a track that IS listed on the album's tracklist — means Tidal has no deliverable asset for it
/// (delisted / rights removed). It is region-independent (geo gating is a 401, not a 404), so no quality
/// tier and no later automatic retry can satisfy the grab.</para>
///
/// <para><b>Honest caveat.</b> The exact permanent trigger (404) is a best-effort classification that has
/// not been live-validated against every Tidal error shape. It is deliberately the ONLY permanent trigger,
/// and the recovery paths (interactive search bypasses suppression; the store TTL self-heals after 30 days)
/// bound the cost of a rare false-permanent. If future live data shows a transient 404 shape, tighten this
/// to additionally require sub-status 2001 rather than loosening any transient case to permanent.</para>
/// </remarks>
public static class TidalStreamRestrictionClassifier
{
    /// <summary>
    /// Classifies a non-success playback-info response. <paramref name="httpStatus"/> is the HTTP status
    /// code; <paramref name="subStatus"/> / <paramref name="userMessage"/> are Tidal's error-body fields
    /// (both optional — absent when the body is empty or unparseable). Never throws; unknown input yields
    /// <see cref="TidalStreamUnavailableReason.Unknown"/> (TRANSIENT).
    /// </summary>
    public static TidalStreamUnavailableReason Classify(int httpStatus, int? subStatus, string? userMessage)
    {
        // "Asset is not ready for playback" — Tidal is still processing the track. Explicitly transient,
        // regardless of the accompanying HTTP status. Checked FIRST (safety bias): an explicit
        // still-processing signal must win even if it ever arrives alongside a 404, so a transient
        // not-ready state can never be upgraded to a permanent rights-removed suppression.
        if (subStatus == 4005)
        {
            return TidalStreamUnavailableReason.NotReady;
        }

        // PERMANENT — the only case that can suppress. See the type remarks for the rationale.
        if (httpStatus == 404)
        {
            return TidalStreamUnavailableReason.RightsRemoved;
        }

        // Everything below is TRANSIENT. The sub-classification is for logging only; none of these
        // reasons is permanent, so none can suppress.
        return httpStatus switch
        {
            401 => TidalStreamUnavailableReason.Authentication,
            403 => TidalStreamUnavailableReason.Forbidden,
            429 => TidalStreamUnavailableReason.RateLimited,
            >= 500 and <= 599 => TidalStreamUnavailableReason.ServerError,
            _ => TidalStreamUnavailableReason.Unknown,
        };
    }
}
