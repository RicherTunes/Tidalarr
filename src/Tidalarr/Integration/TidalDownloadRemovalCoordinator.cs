using Lidarr.Plugin.Common.HostBridge;

namespace Tidalarr.Integration;

internal static class TidalDownloadRemovalCoordinator
{
    public static (bool Removed, bool CancellationSignaled) Remove(
        string downloadId,
        bool deleteData,
        HostBridgeDownloadTrackerStore<HostBridgeDownloadItem> activeDownloads,
        TidalDownloadCancellationRegistry activeDownloadCancellations)
    {
        if (activeDownloads is null) throw new ArgumentNullException(nameof(activeDownloads));
        if (activeDownloadCancellations is null) throw new ArgumentNullException(nameof(activeDownloadCancellations));

        bool cancellationSignaled = false;
        bool removed;
        try
        {
            cancellationSignaled = activeDownloadCancellations.Cancel(downloadId);
        }
        catch
        {
            cancellationSignaled = true;
        }
        finally
        {
            removed = activeDownloads.Remove(downloadId, deleteData, out _);
            if (!removed)
            {
                activeDownloadCancellations.Complete(downloadId);
            }
        }

        return (removed, cancellationSignaled);
    }
}
