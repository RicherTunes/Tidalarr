using Lidarr.Plugin.Common.HostBridge;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// D-5 (silent delete failures): <c>TidalDownloadRemovalCoordinator.Remove</c> used to call
/// <c>HostBridgeDownloadTrackerStore.Remove</c> without the <c>onDeleteError</c> callback Common
/// supports, so a failed <c>deleteData</c> directory delete (locked file, permissions, AV scanner)
/// was swallowed with zero log signal and the orphaned data silently piled up. These tests pin
/// that the coordinator forwards a delete-error callback to the store.
/// </summary>
[Trait("Category", "Unit")]
public class TidalDownloadRemovalCoordinatorDeleteErrorTests
{
    [Fact]
    public void Remove_WhenDeleteFails_InvokesOnDeleteError()
    {
        var registry = new TidalDownloadCancellationRegistry();
        var tracker = new HostBridgeDownloadTrackerStore<HostBridgeDownloadItem>();

        string root = Path.Combine(Path.GetTempPath(), "tidalarr-remove-delete-fail-" + Guid.NewGuid().ToString("N"));
        string dataDir = Path.Combine(root, "data");
        _ = Directory.CreateDirectory(dataDir);
        string filePath = Path.Combine(dataDir, "track.flac");
        File.WriteAllText(filePath, "audio");

        tracker.AddOrReplace(new HostBridgeDownloadItem { DownloadId = "download-1", OutputPath = dataDir });

        var deleteErrors = new List<Exception>();
        FileStream? lockStream = null;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: an open handle with FileShare.None makes the recursive delete throw.
                lockStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            else
            {
                // Unix: removing a directory entry requires write permission on the directory.
                File.SetUnixFileMode(dataDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }

            var result = TidalDownloadRemovalCoordinator.Remove(
                "download-1",
                deleteData: true,
                tracker,
                registry,
                onDeleteError: deleteErrors.Add);

            // The tracker entry is still removed (the queue must clear)...
            Assert.True(result.Removed);
            Assert.Empty(tracker.GetSnapshot());

            // ...but the delete failure must reach the caller instead of vanishing.
            Exception error = Assert.Single(deleteErrors);
            Assert.NotNull(error);
        }
        finally
        {
            lockStream?.Dispose();
            if (!OperatingSystem.IsWindows() && Directory.Exists(dataDir))
            {
                File.SetUnixFileMode(dataDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void Remove_WhenDeleteSucceeds_DoesNotInvokeOnDeleteError()
    {
        var registry = new TidalDownloadCancellationRegistry();
        var tracker = new HostBridgeDownloadTrackerStore<HostBridgeDownloadItem>();

        string root = Path.Combine(Path.GetTempPath(), "tidalarr-remove-delete-ok-" + Guid.NewGuid().ToString("N"));
        string dataDir = Path.Combine(root, "data");
        _ = Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "track.flac"), "audio");

        tracker.AddOrReplace(new HostBridgeDownloadItem { DownloadId = "download-1", OutputPath = dataDir });

        var deleteErrors = new List<Exception>();
        try
        {
            var result = TidalDownloadRemovalCoordinator.Remove(
                "download-1",
                deleteData: true,
                tracker,
                registry,
                onDeleteError: deleteErrors.Add);

            Assert.True(result.Removed);
            Assert.Empty(deleteErrors);
            Assert.False(Directory.Exists(dataDir));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
