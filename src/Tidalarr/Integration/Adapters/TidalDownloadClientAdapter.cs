using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Tidalarr.Integration.Adapters;

public sealed class TidalDownloadClientAdapter : IDownloadClient
{
    private readonly IServiceScope scope;
    private readonly TidalDownloadClient downloadClient;

    public TidalDownloadClientAdapter(IServiceScope scope)
    {
        this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        downloadClient = scope.ServiceProvider.GetRequiredService<TidalDownloadClient>();
    }

    public async ValueTask<PluginValidationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var validation = await downloadClient.InitializeAsync().ConfigureAwait(false);
        return validation.ToPluginValidationResult();
    }

    public async ValueTask<string> EnqueueAlbumDownloadAsync(string albumId, string outputPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await downloadClient.AddDownloadAsync(albumId, outputPath).ConfigureAwait(false);
    }

    public async ValueTask<bool> RemoveDownloadAsync(string downloadId, bool deleteData = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await downloadClient.RemoveDownloadAsync(downloadId, deleteData).ConfigureAwait(false);
    }

    public ValueTask<IReadOnlyList<StreamingDownloadItem>> GetActiveDownloadsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<StreamingDownloadItem> downloads = downloadClient.GetDownloads();
        return ValueTask.FromResult(downloads);
    }

    public ValueTask<StreamingDownloadItem?> GetDownloadAsync(string downloadId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = downloadClient.GetDownload(downloadId);
        return ValueTask.FromResult<StreamingDownloadItem?>(item);
    }

    public async ValueTask DisposeAsync()
    {
        downloadClient.Dispose();

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
