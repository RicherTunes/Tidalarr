using Lidarr.Plugin.Common.Services.Intelligence;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Tidal's adoption of Common's delegate-only <see cref="SearchPlanExecutor"/>: it wires the Tidal
/// specifics (stop policy, result projection, service label) around the shared executor and owns NONE
/// of the loop mechanics — those live in Common, characterized by Common's own SearchPlanExecutorTests.
///
/// <list type="bullet">
///   <item>Stop policy <see cref="SearchStopPolicy.StopAfterFirstTierWithResults"/> — combined → artist-only
///   → album-only fallback: every variant in a tier is attempted, but once a tier yields any album the
///   remaining (lower-priority) fallback tiers are skipped.</item>
///   <item>Per-variant projection: unwrap <see cref="TidalSearchResults.Albums"/> (null-guarded to empty).</item>
///   <item>Service label <c>"Tidal search"</c> so the uniform all-failed
///   <see cref="System.InvalidOperationException"/> message is byte-for-byte the one FetchReleases threw
///   before this adoption.</item>
///   <item>Cancellation propagates: a mid-flight <see cref="System.OperationCanceledException"/> is rethrown
///   by the Common executor rather than being swallowed into a generic all-failed error (the one intended
///   behavior delta vs the former local TidalTieredAlbumSearch).</item>
/// </list>
///
/// <para>Standalone + Lidarr.Core-free on purpose, so the hermetic test gate can drive it without the
/// full Lidarr host assemblies.</para>
/// </summary>
internal static class TidalAlbumSearch
{
    internal static Task<IReadOnlyList<TidalAlbumInfo>> ExecuteAsync(
        IReadOnlyList<IReadOnlyList<string>> tiers,
        Func<string, CancellationToken, Task<TidalSearchResults>> searchAsync,
        Action<string, Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchAsync);

        return SearchPlanExecutor.ExecuteAsync<TidalAlbumInfo>(
            tiers,
            async (q, ct) =>
            {
                TidalSearchResults results = await searchAsync(q, ct).ConfigureAwait(false);
                return results?.Albums ?? Array.Empty<TidalAlbumInfo>();
            },
            SearchStopPolicy.StopAfterFirstTierWithResults,
            onError,
            serviceLabel: "Tidal search",
            cancellationToken);
    }
}
