using Lidarr.Plugin.Common.HostBridge;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Queue 0/0 fix: tracker items created by <c>TidalLidarrDownloadClient.Download</c> never set
/// <c>TotalSize</c>, so <c>ProjectDownloadItems</c> derived <c>RemainingSize</c> from 0 and Lidarr's
/// queue showed every Tidal download as "0/0" regardless of progress. The grabbed release already
/// carries a per-quality size estimate (the indexer computes it via Common's AlbumSizeEstimator),
/// so the item factory seeds <c>TotalSize</c> from <c>ReleaseInfo.Size</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TidalLidarrDownloadClientTotalSizeTests
{
    [Fact]
    public void CreateTrackedDownloadItem_SeedsTotalSizeFromReleaseEstimate()
    {
        TidalDownloadItem item = TidalLidarrDownloadClient.CreateTrackedDownloadItem(
            downloadId: "dl-1",
            albumId: "album-1",
            albumTitle: "Album",
            artistName: "Artist",
            outputPath: "/downloads/Artist/Album",
            totalSizeBytes: 123_456_789);

        Assert.True(item.TotalSize > 0, "the tracker item must carry the release's size estimate or the queue shows 0/0");
        Assert.Equal(123_456_789, item.TotalSize);
        Assert.Equal(HostBridgeDownloadItemStatus.Downloading, item.GetStatus());
        Assert.Equal(0, item.GetProgress());
        Assert.Equal("dl-1", item.DownloadId);
        Assert.Equal("album-1", item.AlbumId);
        Assert.Equal("Album", item.Title);
        Assert.Equal("Artist", item.Artist);
        Assert.Equal("/downloads/Artist/Album", item.OutputPath);
    }

    [Fact]
    public void CreateTrackedDownloadItem_NonPositiveSize_ClampsToZero()
    {
        TidalDownloadItem item = TidalLidarrDownloadClient.CreateTrackedDownloadItem(
            downloadId: "dl-1",
            albumId: "album-1",
            albumTitle: "Album",
            artistName: "Artist",
            outputPath: "/downloads/Artist/Album",
            totalSizeBytes: -42);

        Assert.Equal(0, item.TotalSize);
    }

    [Fact]
    public void ProjectedItem_AfterProgress_DerivesRemainingSizeFromSeededTotalSize()
    {
        TidalDownloadItem item = TidalLidarrDownloadClient.CreateTrackedDownloadItem(
            downloadId: "dl-1",
            albumId: "album-1",
            albumTitle: "Album",
            artistName: "Artist",
            outputPath: "/downloads/Artist/Album",
            totalSizeBytes: 1000);
        item.SetProgress(25);

        var items = TidalLidarrDownloadClient.ProjectDownloadItems(new[] { item }, clientInfo: null);

        Assert.Single(items);
        Assert.Equal(1000, items[0].TotalSize);
        Assert.Equal(750, items[0].RemainingSize);
    }

    /// <summary>
    /// Wiring pin: the Download() item factory must actually route through the seeded factory with
    /// the release's size — a source-level guard because Download() itself needs a live Lidarr host
    /// to drive. Same source-scan technique as DeadSettingsGuardTests.
    /// </summary>
    [Fact]
    public void Download_ItemFactory_SeedsTotalSizeFromReleaseSize()
    {
        string source = File.ReadAllText(FindDownloadClientSource());

        Assert.Contains("CreateTrackedDownloadItem(", source, StringComparison.Ordinal);
        Assert.Contains("remoteAlbum.Release?.Size", source, StringComparison.Ordinal);
    }

    private static string FindDownloadClientSource()
    {
        string relative = Path.Combine("src", "Tidalarr", "Integration", "LidarrNative", "TidalLidarrDownloadClient.cs");
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relative} above {AppContext.BaseDirectory}");
    }
}
