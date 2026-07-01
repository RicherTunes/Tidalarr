using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.TestKit.Compliance;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Tidalarr's adoption of the cross-plugin <c>cover-art-embedding</c> parity axis. Drives the real
/// <see cref="TidalModelMapper.ToStreamingAlbum"/> download-path mapping and asserts the album exposes
/// a fetchable resources.tidal.com cover URL (not the raw <c>CoverArtId</c>) via GetBestCoverArtUrl(),
/// so Common's SimpleDownloadOrchestrator can embed the album cover. Pins the fix for the tidal
/// raw-CoverArtId bug so a future mapper change can't silently regress art-less downloads.
/// </summary>
public sealed class TidalCoverArtEmbeddingTests : CoverArtEmbeddingComplianceTestBase
{
    private readonly TidalModelMapper _mapper = new();

    protected override StreamingAlbum BuildDownloadPathAlbumWithCover() =>
        _mapper.ToStreamingAlbum(new TidalAlbumInfo(
            Id: "id", Title: "Album", Artists: ["Artist"], Tracks: [],
            AvailableQualities: [], ReleaseDate: DateTime.MinValue,
            CoverArtId: "1234-5678-90ab", IsAvailable: true));

    protected override StreamingAlbum BuildDownloadPathAlbumWithoutCover() =>
        _mapper.ToStreamingAlbum(new TidalAlbumInfo(
            Id: "id", Title: "Album", Artists: ["Artist"], Tracks: [],
            AvailableQualities: [], ReleaseDate: DateTime.MinValue,
            CoverArtId: "", IsAvailable: true));
}
