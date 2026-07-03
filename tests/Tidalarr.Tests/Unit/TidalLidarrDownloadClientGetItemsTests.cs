using System.Linq;
using System.Reflection;
using Lidarr.Plugin.Common.HostBridge;
using NzbDrone.Core.Download;
using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Pins the GetItems projection contract for <see cref="TidalLidarrDownloadClient"/>. Lidarr indexes
/// its queue by downloadId and reconciles it against history, so GetItems must never surface the same
/// downloadId twice (the cross-plugin "Tracker snapshot + active-queue duplicate" bug class). Tidal's
/// projection has a single source (the downloadId-keyed tracker), but the dedup is asserted here so a
/// future second-source merge can't silently regress it.
/// </summary>
public sealed class TidalLidarrDownloadClientGetItemsTests
{
    [Fact]
    public void StaticTracker_IsPersistentForPluginConfigRoot()
    {
        var field = typeof(TidalLidarrDownloadClient)
            .GetField("ActiveDownloads", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(typeof(HostBridgeDownloadTrackerStore<TidalDownloadItem>), field!.FieldType);

        var tracker = field.GetValue(null);
        var persistencePathField = field.FieldType.GetField("_persistencePath", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(persistencePathField);
        var persistencePath = Assert.IsType<string>(persistencePathField!.GetValue(tracker));
        Assert.EndsWith(
            "/Tidalarr/download-tracker.json",
            persistencePath.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static HostBridgeDownloadItem Item(string downloadId, HostBridgeDownloadItemStatus status, double progress = 0, long totalSize = 0)
    {
        var item = new HostBridgeDownloadItem
        {
            DownloadId = downloadId,
            Title = "Album",
            Artist = "Artist",
            OutputPath = "/downloads/Artist/Album",
            TotalSize = totalSize
        };
        item.SetStatus(status);
        item.SetProgress(progress);
        return item;
    }

    [Fact]
    public void ProjectDownloadItems_DistinctIds_ProjectsOneToOne()
    {
        var snapshot = new[]
        {
            Item("dl-1", HostBridgeDownloadItemStatus.Downloading),
            Item("dl-2", HostBridgeDownloadItemStatus.Completed),
            Item("dl-3", HostBridgeDownloadItemStatus.Failed),
        };

        var items = TidalLidarrDownloadClient.ProjectDownloadItems(snapshot, clientInfo: null);

        Assert.Equal(3, items.Count);
        Assert.Equal(new[] { "dl-1", "dl-2", "dl-3" }, items.Select(i => i.DownloadId));
        // DownloadId-distinct is the load-bearing invariant.
        Assert.Equal(items.Count, items.Select(i => i.DownloadId).Distinct().Count());

        // Completion contract: a Failed host-bridge status still projects to Lidarr's Failed status.
        // Terminal-release suppression is a pure search-side side effect and must never soften this
        // (an incomplete/failed album must keep reporting Failed so Lidarr can fall back to another source).
        var failed = items.Single(i => i.DownloadId == "dl-3");
        Assert.Equal(NzbDrone.Core.Download.DownloadItemStatus.Failed, failed.Status);
    }

    [Fact]
    public void ProjectDownloadItems_DuplicateDownloadId_ReportedOnce()
    {
        // Same downloadId appearing twice in the source (simulating a hypothetical second-source
        // merge) must collapse to a single host item.
        var snapshot = new[]
        {
            Item("dup", HostBridgeDownloadItemStatus.Downloading),
            Item("dup", HostBridgeDownloadItemStatus.Completed),
        };

        var items = TidalLidarrDownloadClient.ProjectDownloadItems(snapshot, clientInfo: null);

        Assert.Single(items);
        Assert.Equal("dup", items[0].DownloadId);
    }

    [Fact]
    public void ProjectDownloadItems_BlankDownloadId_Dropped()
    {
        var snapshot = new[]
        {
            Item("", HostBridgeDownloadItemStatus.Downloading),
            Item("real", HostBridgeDownloadItemStatus.Downloading),
        };

        var items = TidalLidarrDownloadClient.ProjectDownloadItems(snapshot, clientInfo: null);

        Assert.Single(items);
        Assert.Equal("real", items[0].DownloadId);
    }

    [Theory]
    [InlineData(HostBridgeDownloadItemStatus.Completed, DownloadItemStatus.Completed)]
    [InlineData(HostBridgeDownloadItemStatus.Failed, DownloadItemStatus.Failed)]
    [InlineData(HostBridgeDownloadItemStatus.Cancelled, DownloadItemStatus.Warning)]
    [InlineData(HostBridgeDownloadItemStatus.Downloading, DownloadItemStatus.Downloading)]
    [InlineData(HostBridgeDownloadItemStatus.Queued, DownloadItemStatus.Queued)]
    public void ProjectDownloadItems_MapsStatus(HostBridgeDownloadItemStatus source, DownloadItemStatus expected)
    {
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(new[] { Item("dl", source) }, clientInfo: null);

        Assert.Single(items);
        Assert.Equal(expected, items[0].Status);
    }

    [Fact]
    public void ProjectDownloadItems_ComputesRemainingSizeFromProgress()
    {
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(
            new[] { Item("dl", HostBridgeDownloadItemStatus.Downloading, progress: 25, totalSize: 1000) },
            clientInfo: null);

        Assert.Single(items);
        Assert.Equal(1000, items[0].TotalSize);
        Assert.Equal(750, items[0].RemainingSize);
    }

    // Host-contract: Lidarr uses CanMoveFiles to choose move-vs-copy import and CanBeRemoved to emit
    // the post-import remove event. Both default to FALSE, so leaving them unset makes a completed
    // download import copy-only and never get cleaned up (the source piles up). qobuz/amazon set both;
    // tidal shipped without them (DownloadClientItem ctor at ProjectDownloadItems).
    [Fact]
    public void ProjectDownloadItems_CompletedItem_SetsCanMoveFilesAndCanBeRemoved()
    {
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(
            new[] { Item("dl", HostBridgeDownloadItemStatus.Completed) }, clientInfo: null);

        Assert.Single(items);
        Assert.True(items[0].CanMoveFiles,
            "a completed download must set CanMoveFiles or Lidarr imports copy-only and never cleans up the source");
        Assert.True(items[0].CanBeRemoved,
            "a completed download must set CanBeRemoved or Lidarr never emits the post-import remove event");
    }

    [Theory]
    [InlineData(HostBridgeDownloadItemStatus.Downloading)]
    [InlineData(HostBridgeDownloadItemStatus.Queued)]
    public void ProjectDownloadItems_InProgressItem_CannotMoveOrRemove(HostBridgeDownloadItemStatus status)
    {
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(
            new[] { Item("dl", status) }, clientInfo: null);

        Assert.Single(items);
        Assert.False(items[0].CanMoveFiles, "an in-progress download must not be move-imported");
        Assert.False(items[0].CanBeRemoved, "an in-progress download must not be removed");
    }

    [Theory]
    [InlineData(HostBridgeDownloadItemStatus.Failed)]
    [InlineData(HostBridgeDownloadItemStatus.Cancelled)]
    public void ProjectDownloadItems_TerminalFailure_CanBeRemovedButNotMoved(HostBridgeDownloadItemStatus status)
    {
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(
            new[] { Item("dl", status) }, clientInfo: null);

        Assert.Single(items);
        Assert.False(items[0].CanMoveFiles, "a failed/cancelled download has no complete file set to move-import");
        Assert.True(items[0].CanBeRemoved, "a terminal failure must be removable so the queue can be cleared");
    }

    // T-failure-message: Lidarr's queue previously showed a bare "Failed" status for every tidal
    // download failure with zero context on *why* — ProjectDownloadItems never set
    // DownloadClientItem.Message. TidalDownloadItem (a plugin-local HostBridgeDownloadItem
    // subclass) now carries a Message set at the two failure sites in Download(); this pins the
    // projection half of that fix.
    private static TidalDownloadItem TidalItem(string downloadId, HostBridgeDownloadItemStatus status, string? message = null)
    {
        var item = new TidalDownloadItem
        {
            DownloadId = downloadId,
            Title = "Album",
            Artist = "Artist",
            OutputPath = "/downloads/Artist/Album",
            Message = message,
        };
        item.SetStatus(status);
        return item;
    }

    [Fact]
    public void ProjectDownloadItems_FailedItem_WithMessage_ProjectsToHostMessage()
    {
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(
            new[] { TidalItem("dl", HostBridgeDownloadItemStatus.Failed, "1 track failed: HTTP 403 Forbidden") },
            clientInfo: null);

        Assert.Single(items);
        Assert.Equal("1 track failed: HTTP 403 Forbidden", items[0].Message);
    }

    [Fact]
    public void ProjectDownloadItems_FailedItem_RedactsSensitiveMessage()
    {
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(
            new[]
            {
                TidalItem(
                    "dl",
                    HostBridgeDownloadItemStatus.Failed,
                    "Failed https://media.tidal.com/seg.m4s?token=SECRET&signature=PRIVATE"),
            },
            clientInfo: null);

        Assert.Single(items);
        Assert.DoesNotContain("SECRET", items[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE", items[0].Message, StringComparison.Ordinal);
        Assert.Contains("https://media.tidal.com/seg.m4s?[REDACTED]", items[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDownloadItems_ItemWithoutMessage_LeavesHostMessageNull()
    {
        // Regression guard: a plain base HostBridgeDownloadItem (as pre-existing tests construct)
        // or a TidalDownloadItem with no Message set must not fabricate a message.
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(
            new[] { Item("dl", HostBridgeDownloadItemStatus.Completed) }, clientInfo: null);

        Assert.Single(items);
        Assert.Null(items[0].Message);
    }

    [Fact]
    public void ProjectDownloadItems_EmptyMessage_TreatedAsNoMessage()
    {
        var items = TidalLidarrDownloadClient.ProjectDownloadItems(
            new[] { TidalItem("dl", HostBridgeDownloadItemStatus.Failed, message: "") },
            clientInfo: null);

        Assert.Single(items);
        Assert.Null(items[0].Message);
    }

    [Theory]
    [InlineData(0, 1, "Download failed: no track results returned (no track IDs resolved from the API).")]
    public void BuildFailureMessage_NoTrackResults_ReportsNoTrackIds(int trackResultCount, int fileCount, string expectedPrefix)
    {
        string message = TidalLidarrDownloadClient.BuildFailureMessage([], fileCount, trackResultCount);
        Assert.Equal(expectedPrefix, message);
    }

    [Fact]
    public void BuildFailureMessage_SingleFailedTrack_UsesItsErrorMessage()
    {
        Lidarr.Plugin.Common.Interfaces.TrackDownloadResult failed = new()
        {
            TrackId = "t1",
            Success = false,
            ErrorMessage = "HTTP 403 Forbidden",
        };

        string message = TidalLidarrDownloadClient.BuildFailureMessage([failed], fileCount: 0, trackResultCount: 1);

        Assert.Equal("1 track failed: HTTP 403 Forbidden", message);
    }

    [Fact]
    public void BuildFailureMessage_RedactsSensitiveUrlsFromFailedTrackReason()
    {
        Lidarr.Plugin.Common.Interfaces.TrackDownloadResult failed = new()
        {
            TrackId = "t1",
            Success = false,
            ErrorMessage = "Failed https://media.tidal.com/seg.m4s?token=SECRET&signature=PRIVATE",
        };

        string message = TidalLidarrDownloadClient.BuildFailureMessage([failed], fileCount: 0, trackResultCount: 1);

        Assert.DoesNotContain("SECRET", message, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE", message, StringComparison.Ordinal);
        Assert.Contains("https://media.tidal.com/seg.m4s?[REDACTED]", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFailureMessage_MultipleFailedTracks_ReportsCountAndFirstReason()
    {
        Lidarr.Plugin.Common.Interfaces.TrackDownloadResult first = new() { TrackId = "t1", Success = false, ErrorMessage = "Truncated response" };
        Lidarr.Plugin.Common.Interfaces.TrackDownloadResult second = new() { TrackId = "t2", Success = false, ErrorMessage = "Timeout" };

        string message = TidalLidarrDownloadClient.BuildFailureMessage([first, second], fileCount: 0, trackResultCount: 2);

        Assert.Equal("2 tracks failed (first: Truncated response)", message);
    }

    [Fact]
    public void BuildFailureMessage_ZeroFilesNoFailedTracks_ReportsFileCountMismatch()
    {
        string message = TidalLidarrDownloadClient.BuildFailureMessage([], fileCount: 0, trackResultCount: 3);

        Assert.Equal("Download failed: 0 files produced from 3 track result(s).", message);
    }
}
