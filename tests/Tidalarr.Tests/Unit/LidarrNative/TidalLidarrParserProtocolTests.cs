using NzbDrone.Core.Parser.Model;
using Tidalarr.Core.Models;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit.LidarrNative;

public class TidalLidarrParserProtocolTests
{
    private static TidalAlbumInfo CreateAlbum(
        string id = "al1",
        string title = "Album",
        string artist = "Artist",
        TidalQuality? quality = null,
        DateTime? releaseDate = null)
    {
        return new TidalAlbumInfo(
            Id: id,
            Title: title,
            Artists: new List<string> { artist },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { quality ?? TidalQuality.Lossless },
            ReleaseDate: releaseDate ?? new DateTime(2024, 6, 15),
            CoverArtId: "cover",
            IsAvailable: true);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_ShouldSetDownloadProtocol()
    {
        TidalAlbumInfo album = CreateAlbum();

        ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.NotNull(release);
        Assert.Equal(nameof(TidalarrDownloadProtocol), release.DownloadProtocol);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_ShouldIncludeQualityInGuid()
    {
        TidalAlbumInfo album = CreateAlbum();

        ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.Contains(":Lossless", release.Guid);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_ShouldIncludeQualityInDownloadUrl()
    {
        TidalAlbumInfo album = CreateAlbum();

        ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.Contains("quality=Lossless", release.DownloadUrl);
    }

    [Theory]
    [InlineData(TidalQuality.Lossless, "[FLAC] [WEB]")]
    [InlineData(TidalQuality.HiRes, "[FLAC] [HIRES] [WEB]")]
    [InlineData(TidalQuality.High, "[AAC] [320] [WEB]")]
    [InlineData(TidalQuality.Low, "[AAC] [96] [WEB]")]
    public void ConvertToReleaseInfoStatic_TitleFormat_MatchesQuality(TidalQuality quality, string expectedSuffix)
    {
        TidalAlbumInfo album = CreateAlbum(quality: quality);

        ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.EndsWith(expectedSuffix, release.Title);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_ShouldIncludeYearInTitle()
    {
        TidalAlbumInfo album = CreateAlbum(releaseDate: new DateTime(2024, 6, 15));

        ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.Contains("(2024)", release.Title);
    }

    [Fact]
    public void ConvertToReleaseInfosStatic_ProducesFourReleases()
    {
        TidalAlbumInfo album = CreateAlbum();

        List<ReleaseInfo> releases = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(album)];

        Assert.Equal(4, releases.Count);
    }

    [Fact]
    public void ConvertToReleaseInfosStatic_AllReleasesHaveUniqueGuids()
    {
        TidalAlbumInfo album = CreateAlbum();

        List<ReleaseInfo> releases = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(album)];

        Assert.Equal(4, releases.Select(r => r.Guid).Distinct().Count());
    }

    [Fact]
    public void ConvertToReleaseInfosStatic_AllReleasesHaveDistinctTitles()
    {
        TidalAlbumInfo album = CreateAlbum();

        List<ReleaseInfo> releases = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(album)];

        // Low=[AAC] [96] [WEB], High=[AAC] [320] [WEB], Lossless=[FLAC] [WEB], HiRes=[FLAC] [HIRES] [WEB]
        Assert.Equal(4, releases.Select(r => r.Title).Distinct().Count());
    }

    [Fact]
    public void ConvertToReleaseInfosStatic_AllReleasesHaveDownloadProtocol()
    {
        TidalAlbumInfo album = CreateAlbum();

        List<ReleaseInfo> releases = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(album)];

        Assert.All(releases, r => Assert.Equal(nameof(TidalarrDownloadProtocol), r.DownloadProtocol));
    }

    [Fact]
    public void ConvertToReleaseInfosStatic_AllReleasesHaveQualityInDownloadUrl()
    {
        TidalAlbumInfo album = CreateAlbum();

        List<ReleaseInfo> releases = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(album)];

        Assert.All(releases, r => Assert.Contains("quality=", r.DownloadUrl));
    }
}
