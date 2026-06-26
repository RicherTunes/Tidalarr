using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Runs ordered search tiers and stops at the first tier that yields any albums.
///
/// <para>This is the execution half of the combined → artist-only → album-only fallback the
/// indexer needs: every variant within a tier is attempted, but once a tier returns at least one
/// album the remaining (lower-priority) fallback tiers are skipped. A tier whose every query
/// throws does NOT short-circuit — execution falls through so a transient failure on the
/// combined query can still be rescued by the artist-only fallback.</para>
/// </summary>
internal static class TidalTieredAlbumSearch
{
    /// <summary>
    /// Result of a tiered search: the albums from the winning tier plus bookkeeping the caller
    /// uses to distinguish "no matches" (Succeeded &gt; 0, no albums) from "every call failed"
    /// (Succeeded == 0 with a <see cref="LastError"/>).
    /// </summary>
    internal sealed record Outcome(List<TidalAlbumInfo> Albums, int Attempted, int Succeeded, Exception? LastError);

    internal static async Task<Outcome> RunAsync(
        IReadOnlyList<IReadOnlyList<string>> tiers,
        Func<string, CancellationToken, Task<TidalSearchResults>> searchAsync,
        Action<string, Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchAsync);

        List<TidalAlbumInfo> albums = [];
        int attempted = 0;
        int succeeded = 0;
        Exception? lastError = null;

        foreach (IReadOnlyList<string> tier in tiers ?? [])
        {
            int albumsBeforeTier = albums.Count;

            foreach (string query in tier)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempted++;
                try
                {
                    TidalSearchResults results = await searchAsync(query, cancellationToken).ConfigureAwait(false);
                    succeeded++;
                    if (results?.Albums is { Count: > 0 } found)
                    {
                        albums.AddRange(found);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    onError?.Invoke(query, ex);
                }
            }

            // A tier that produced results short-circuits the remaining fallback tiers.
            if (albums.Count > albumsBeforeTier)
            {
                break;
            }
        }

        return new Outcome(albums, attempted, succeeded, lastError);
    }
}
