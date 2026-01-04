using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

public class TidalLidarrIndexerSizeEstimationTests
{
    [Fact]
    public void ConvertToReleaseInfoStatic_WhenTracksEmpty_DefaultsToNonZeroEstimate()
    {
        TidalAlbumInfo album = new TidalAlbumInfo(
            Id: "123",
            Title: "Test Album",
            Artists: ["Test Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.Lossless],
            ReleaseDate: new DateTime(2020, 1, 1),
            CoverArtId: "art",
            IsAvailable: true);

        NzbDrone.Core.Parser.Model.ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.NotNull(release);
        Assert.True(release.Size > 0);
        Assert.Equal(360_000_000L, release.Size);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_SetsDownloadProtocol()
    {
        TidalAlbumInfo album = new TidalAlbumInfo(
            Id: "123",
            Title: "Test Album",
            Artists: ["Test Artist"],
            Tracks: [],
            AvailableQualities: [TidalQuality.Lossless],
            ReleaseDate: new DateTime(2020, 1, 1),
            CoverArtId: "art",
            IsAvailable: true);

        NzbDrone.Core.Parser.Model.ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.NotNull(release);
        Assert.Equal(nameof(TidalarrDownloadProtocol), release.DownloadProtocol);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_WhenTracksPresent_UsesTrackCountForEstimate()
    {
        TidalTrackInfo[] tracks =
        [
            new TidalTrackInfo("t1", "One", ["Test Artist"], "123", "Test Album", 1, 1, TidalQuality.Lossless, true, new DateTime(2020, 1, 1)),
            new TidalTrackInfo("t2", "Two", ["Test Artist"], "123", "Test Album", 2, 1, TidalQuality.Lossless, true, new DateTime(2020, 1, 1)),
            new TidalTrackInfo("t3", "Three", ["Test Artist"], "123", "Test Album", 3, 1, TidalQuality.Lossless, true, new DateTime(2020, 1, 1)),
        ];

        TidalAlbumInfo album = new TidalAlbumInfo(
            Id: "123",
            Title: "Test Album",
            Artists: ["Test Artist"],
            Tracks: tracks,
            AvailableQualities: [TidalQuality.Lossless],
            ReleaseDate: new DateTime(2020, 1, 1),
            CoverArtId: "art",
            IsAvailable: true);

        NzbDrone.Core.Parser.Model.ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.NotNull(release);
        Assert.True(release.Size > 0);
        Assert.Equal(90_000_000L, release.Size);
    }
}
