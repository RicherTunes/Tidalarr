using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.TestKit.Compliance;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Tidalarr's adoption of the cross-plugin <c>cover-art-embedding</c> parity axis. Drives the real
/// <see cref="TidalModelMapper.ToStreamingTracks"/> download-path mapping and asserts the album exposes
/// a fetchable resources.tidal.com cover URL (not the raw <c>CoverArtId</c>) via GetBestCoverArtUrl(),
/// so Common's SimpleDownloadOrchestrator can embed the album cover. Pins the fix for the tidal
/// raw-CoverArtId bug so a future mapper change can't silently regress art-less downloads.
/// </summary>
public sealed class TidalCoverArtEmbeddingTests : CoverArtEmbeddingComplianceTestBase
{
    private readonly TidalModelMapper _mapper = new();

    private StreamingAlbum DownloadPathAlbum(string coverArtId) =>
        _mapper.ToStreamingTracks(new TidalAlbumInfo(
            Id: "id", Title: "Album", Artists: ["Artist"],
            Tracks: [new TidalTrackInfo("t1", "Song", ["Artist"], "id", "Album", 1, 180, TidalQuality.High, true, DateTime.MinValue)],
            AvailableQualities: [], ReleaseDate: DateTime.MinValue,
            CoverArtId: coverArtId, IsAvailable: true))[0].Album;

    protected override StreamingAlbum BuildDownloadPathAlbumWithCover() =>
        DownloadPathAlbum("1a2b3c4d-5e6f-7890-ab12-cd34ef567890");

    protected override StreamingAlbum BuildDownloadPathAlbumWithoutCover() =>
        DownloadPathAlbum(string.Empty);
}
