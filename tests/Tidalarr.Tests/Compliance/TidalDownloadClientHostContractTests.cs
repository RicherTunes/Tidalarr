using System.Collections.Generic;
using System.Linq;
using Lidarr.Plugin.Common.HostBridge;
using Lidarr.Plugin.Common.TestKit.Compliance;
using NzbDrone.Core.Download;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Compliance;

/// <summary>
/// Tidal's adoption of Common's shared download-client host-contract guard. Maps the REAL
/// <see cref="TidalLidarrDownloadClient.ProjectDownloadItems"/> output onto the host-free
/// <see cref="HostDownloadItemView"/> so the cross-plugin invariants (CanMoveFiles/CanBeRemoved on a
/// completed download, client id != 0, Cancelled not reported as in-progress, dedup by DownloadId) are
/// enforced here and can never silently regress again.
/// </summary>
public sealed class TidalDownloadClientHostContractTests : DownloadClientHostContractTestBase
{
    private static readonly DownloadClientItemClientInfo ClientInfo = new() { Id = 7, Name = "Tidalarr" };

    private static HostBridgeDownloadItem Item(string id, HostBridgeDownloadItemStatus status)
    {
        var item = new HostBridgeDownloadItem
        {
            DownloadId = id,
            Title = "Album",
            Artist = "Artist",
            OutputPath = "/downloads/Artist/Album",
        };
        item.SetStatus(status);
        return item;
    }

    private static HostDownloadItemView Map(DownloadClientItem d) =>
        new(d.DownloadId, d.DownloadClientInfo?.Id ?? 0, d.Status.ToString(), d.CanMoveFiles, d.CanBeRemoved);

    private static HostDownloadItemView Project1(HostBridgeDownloadItemStatus status) =>
        Map(TidalLidarrDownloadClient.ProjectDownloadItems(new[] { Item("dl", status) }, ClientInfo).Single());

    protected override HostDownloadItemView Completed() => Project1(HostBridgeDownloadItemStatus.Completed);

    protected override HostDownloadItemView Failed() => Project1(HostBridgeDownloadItemStatus.Failed);

    protected override HostDownloadItemView? Cancelled() => Project1(HostBridgeDownloadItemStatus.Cancelled);

    protected override IReadOnlyList<HostDownloadItemView> DuplicateDownloadId(string downloadId) =>
        TidalLidarrDownloadClient.ProjectDownloadItems(
            new[]
            {
                Item(downloadId, HostBridgeDownloadItemStatus.Completed),
                Item(downloadId, HostBridgeDownloadItemStatus.Downloading),
            },
            ClientInfo).Select(Map).ToList();
}
