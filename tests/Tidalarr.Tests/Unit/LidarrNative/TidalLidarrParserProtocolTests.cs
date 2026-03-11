using NzbDrone.Core.Parser.Model;
using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit.LidarrNative;

public class TidalLidarrParserProtocolTests
{
    [Fact]
    public void ConvertToReleaseInfoStatic_ShouldSetDownloadProtocol()
    {
        var album = new TidalAlbumInfo(
            Id: "al1",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { TidalQuality.Lossless },
            ReleaseDate: DateTime.UtcNow.Date,
            CoverArtId: "cover",
            IsAvailable: true);

        ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.NotNull(release);
        Assert.Equal(nameof(TidalarrDownloadProtocol), release.DownloadProtocol);
    }
}
