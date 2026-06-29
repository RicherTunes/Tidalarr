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
        Assert.Equal(typeof(HostBridgeDownloadTrackerStore<HostBridgeDownloadItem>), field!.FieldType);

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
}
