using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Tidalarr.Integration.Adapters;

public sealed class TidalIndexerAdapter : IIndexer
{
    private readonly IServiceScope scope;
    private readonly TidalIndexer indexer;

    public TidalIndexerAdapter(IServiceScope scope)
    {
        this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        indexer = scope.ServiceProvider.GetRequiredService<TidalIndexer>();
    }

    public async ValueTask<PluginValidationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var validation = await indexer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return validation.ToPluginValidationResult();
    }

    public async ValueTask<IReadOnlyList<StreamingAlbum>> SearchAlbumsAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = await indexer.SearchAsync(query).ConfigureAwait(false);
        return results;
    }

    public async ValueTask<IReadOnlyList<StreamingTrack>> SearchTracksAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = await indexer.SearchTracksInternalAsync(query).ConfigureAwait(false);
        return results;
    }

    public async ValueTask<StreamingAlbum?> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        return await indexer.GetAlbumDetailsInternalAsync(albumId).ConfigureAwait(false);
    }

    public IAsyncEnumerable<StreamingAlbum> SearchAlbumsStreamAsync(string query, CancellationToken cancellationToken = default)
    {
        return indexer.SearchStreamAsync(query, cancellationToken);
    }

    public async IAsyncEnumerable<StreamingTrack> SearchTracksStreamAsync(string query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tracks = await indexer.SearchTracksInternalAsync(query).ConfigureAwait(false);
        foreach (var track in tracks ?? Enumerable.Empty<StreamingTrack>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return track;
        }
    }

    public async ValueTask DisposeAsync()
    {
        indexer.Dispose();

        if (scope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            scope.Dispose();
        }
    }
}

