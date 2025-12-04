using System.Runtime.CompilerServices;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Tidalarr.Integration.Adapters;

public sealed class TidalIndexerAdapter(IServiceScope scope) : IIndexer
{
    private readonly IServiceScope scope = scope ?? throw new ArgumentNullException(nameof(scope));
    private readonly TidalIndexer indexer = scope.ServiceProvider.GetRequiredService<TidalIndexer>();

    public async ValueTask<PluginValidationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        FluentValidation.Results.ValidationResult validation = await this.indexer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return validation.ToPluginValidationResult();
    }

    public async ValueTask<IReadOnlyList<StreamingAlbum>> SearchAlbumsAsync(string query, CancellationToken cancellationToken = default)
    {
        List<StreamingAlbum> results = await this.indexer.SearchAsync(query).ConfigureAwait(false);
        return results;
    }

    public async ValueTask<IReadOnlyList<StreamingTrack>> SearchTracksAsync(string query, CancellationToken cancellationToken = default)
    {
        List<StreamingTrack> results = await this.indexer.SearchTracksInternalAsync(query).ConfigureAwait(false);
        return results;
    }

    public async ValueTask<StreamingAlbum?> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
    {
        return await this.indexer.GetAlbumDetailsInternalAsync(albumId).ConfigureAwait(false);
    }

    public IAsyncEnumerable<StreamingAlbum> SearchAlbumsStreamAsync(string query, CancellationToken cancellationToken = default)
    {
        return this.indexer.SearchStreamAsync(query, cancellationToken);
    }

    public async IAsyncEnumerable<StreamingTrack> SearchTracksStreamAsync(string query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<StreamingTrack> tracks = await this.indexer.SearchTracksInternalAsync(query).ConfigureAwait(false);
        foreach (StreamingTrack track in tracks ?? Enumerable.Empty<StreamingTrack>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return track;
        }
    }

    public async ValueTask DisposeAsync()
    {
        this.indexer.Dispose();

        if (this.scope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            this.scope.Dispose();
        }
    }
}

