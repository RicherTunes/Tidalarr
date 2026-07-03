namespace Tidalarr.Core.Exceptions;

/// <summary>
/// Categorized reason a Tidal track's stream could not be resolved. Drives terminal-release suppression:
/// only <see cref="RightsRemoved"/> is treated as PERMANENT (a re-grab of the exact same catalog entry can
/// never succeed), so only it can cause an album's releases to be withheld from future automatic searches.
///
/// <para><b>Safety bias.</b> Mis-classifying a TRANSIENT failure as permanent permanently hides a
/// recoverable album (a false negative — the album is never re-grabbed automatically). That is a strictly
/// worse failure than the bounded re-grab loop suppression exists to stop, so every ambiguous, unknown, or
/// unrecognized signal MUST default to a non-permanent reason. See
/// <see cref="Tidalarr.Domain.Streaming.TidalStreamRestrictionClassifier"/>.</para>
/// </summary>
public enum TidalStreamUnavailableReason
{
    /// <summary>
    /// Default / safety fallback. Unclassified or ambiguous — treated as TRANSIENT (never suppressed).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// PERMANENT. Tidal has no deliverable playable asset for a track that IS listed on the album — the
    /// catalog entry has been delisted / had its streaming rights removed. No quality tier can satisfy the
    /// grab, so the album is withheld from future automatic searches (recoverable via interactive search).
    /// </summary>
    RightsRemoved,

    /// <summary>TRANSIENT. Auth needs refresh (HTTP 401) — succeeds again after re-authentication.</summary>
    Authentication,

    /// <summary>
    /// TRANSIENT. Forbidden (HTTP 403) — region / subscription-tier / rights gate that may lift (region
    /// change, catalog rollout, tier upgrade, or a lower quality tier still succeeding). Deliberately NOT
    /// permanent, matching qobuz's decision to exclude geo restrictions from suppression.
    /// </summary>
    Forbidden,

    /// <summary>TRANSIENT. Asset still being processed by Tidal (sub-status 4005) — retryable later.</summary>
    NotReady,

    /// <summary>TRANSIENT. Rate limited (HTTP 429).</summary>
    RateLimited,

    /// <summary>TRANSIENT. Tidal-side server error (HTTP 5xx).</summary>
    ServerError,

    /// <summary>TRANSIENT. Network / timeout / connection failure.</summary>
    Network,
}

/// <summary>
/// Classification helpers for <see cref="TidalStreamUnavailableReason"/>.
/// </summary>
public static class TidalStreamUnavailableReasonExtensions
{
    /// <summary>
    /// True only for a reason that will NEVER lift for a re-grab of the exact same Tidal catalog entry.
    /// Used solely to decide whether an album-download failure suppresses the album's releases from future
    /// automatic indexer searches — it does NOT change the album-completion decision (an incomplete album
    /// still always reports Failed to Lidarr; see CLAUDE.md "Terminal release suppression").
    ///
    /// <para>Only <see cref="TidalStreamUnavailableReason.RightsRemoved"/> qualifies. Region/tier
    /// (<see cref="TidalStreamUnavailableReason.Forbidden"/>) is deliberately excluded because
    /// availability can change (region change, catalog rollout, subscription upgrade, or a lower quality
    /// tier still succeeding) — permanently hiding a release that might become available is a worse failure
    /// mode than a bounded re-grab loop.</para>
    /// </summary>
    public static bool IsPermanent(this TidalStreamUnavailableReason reason)
        => reason == TidalStreamUnavailableReason.RightsRemoved;
}
