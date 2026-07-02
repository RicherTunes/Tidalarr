using System.Collections.Concurrent;
using Lidarr.Plugin.Common.HostBridge;

namespace Tidalarr.Integration;

internal sealed class TidalDownloadCancellationRegistry
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sources = new();

    public HostBridgeDownloadCancellationRegistration Register(string downloadId)
    {
        if (string.IsNullOrWhiteSpace(downloadId))
        {
            throw new ArgumentException("Download id must be non-empty.", nameof(downloadId));
        }

        var source = new CancellationTokenSource();
        if (_sources.TryAdd(downloadId, source))
        {
            return new HostBridgeDownloadCancellationRegistration(
                source.Token,
                () => Complete(downloadId));
        }

        source.Dispose();
        throw new InvalidOperationException($"Cancellation source already exists for download '{downloadId}'.");
    }

    public bool Cancel(string downloadId)
    {
        if (string.IsNullOrWhiteSpace(downloadId) || !_sources.TryGetValue(downloadId, out var source))
        {
            return false;
        }

        try
        {
            source.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (AggregateException)
        {
            return true;
        }
    }

    public bool Complete(string downloadId)
    {
        if (string.IsNullOrWhiteSpace(downloadId) || !_sources.TryRemove(downloadId, out var source))
        {
            return false;
        }

        source.Dispose();
        return true;
    }
}
