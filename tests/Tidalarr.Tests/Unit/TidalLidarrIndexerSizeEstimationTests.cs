using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

public class TidalLidarrIndexerSizeEstimationTests
{
    [Fact]
    public void ConvertToReleaseInfoStatic_WhenTracksEmpty_DefaultsToNonZeroEstimate()
    {
        var album = new TidalAlbumInfo(
            Id: "123",
            Title: "Test Album",
            Artists: new[] { "Test Artist" },
            Tracks: Array.Empty<TidalTrackInfo>(),
            AvailableQualities: new[] { TidalQuality.Lossless },
            ReleaseDate: new DateTime(2020, 1, 1),
            CoverArtId: "art",
            IsAvailable: true);

        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.NotNull(release);
        Assert.True(release.Size > 0);
        Assert.Equal(360_000_000L, release.Size);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_WhenTracksPresent_UsesTrackCountForEstimate()
    {
        var tracks = new[]
        {
            new TidalTrackInfo("t1", "One", new[] { "Test Artist" }, "123", "Test Album", 1, 1, TidalQuality.Lossless, true, new DateTime(2020, 1, 1)),
            new TidalTrackInfo("t2", "Two", new[] { "Test Artist" }, "123", "Test Album", 2, 1, TidalQuality.Lossless, true, new DateTime(2020, 1, 1)),
            new TidalTrackInfo("t3", "Three", new[] { "Test Artist" }, "123", "Test Album", 3, 1, TidalQuality.Lossless, true, new DateTime(2020, 1, 1)),
        };

        var album = new TidalAlbumInfo(
            Id: "123",
            Title: "Test Album",
            Artists: new[] { "Test Artist" },
            Tracks: tracks,
            AvailableQualities: new[] { TidalQuality.Lossless },
            ReleaseDate: new DateTime(2020, 1, 1),
            CoverArtId: "art",
            IsAvailable: true);

        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.NotNull(release);
        Assert.True(release.Size > 0);
        Assert.Equal(90_000_000L, release.Size);
    }
}
