using Lidarr.Plugin.Common.HostBridge;

namespace Tidalarr.Integration;

internal static class TidalDownloadRemovalCoordinator
{
    /// <param name="onDeleteError">
    /// Invoked when <paramref name="deleteData"/> is set and deleting the item's output directory
    /// fails (locked file, permissions, AV scanner). Forwarded to
    /// <see cref="HostBridgeDownloadTrackerStore{TItem}.Remove"/> so the failure is surfaced (D-5)
    /// instead of being swallowed silently; without it, orphaned data piles up with zero log signal.
    /// </param>
    public static (bool Removed, bool CancellationSignaled) Remove<TItem>(
        string downloadId,
        bool deleteData,
        HostBridgeDownloadTrackerStore<TItem> activeDownloads,
        TidalDownloadCancellationRegistry activeDownloadCancellations,
        Action<Exception>? onDeleteError = null)
        where TItem : HostBridgeDownloadItem
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
            removed = activeDownloads.Remove(downloadId, deleteData, out _, onDeleteError);
            if (!removed)
            {
                activeDownloadCancellations.Complete(downloadId);
            }
        }

        return (removed, cancellationSignaled);
    }
}
