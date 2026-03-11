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

    // --- Round 3: Edge case tests ---

    [Fact]
    public void ConvertToReleaseInfosStatic_NullAlbum_ReturnsEmpty()
    {
        List<ReleaseInfo> releases = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(null!)];

        Assert.Empty(releases);
    }

    [Fact]
    public void ConvertToReleaseInfosStatic_EmptyAlbumId_SkipsAlbum()
    {
        // Album with empty ID should not produce releases with semantically invalid GUIDs
        TidalAlbumInfo album = CreateAlbum(id: "");

        List<ReleaseInfo> releases = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(album)];

        Assert.Empty(releases);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_NullAlbum_ReturnsNull()
    {
        ReleaseInfo? release = TidalLidarrParser.ConvertToReleaseInfoStatic(null!);

        Assert.Null(release);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_EmptyAvailableQualities_DefaultsToLossless()
    {
        // With empty AvailableQualities list, should default to Lossless, NOT Low (enum default 0)
        TidalAlbumInfo album = new(
            Id: "al1",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),  // empty!
            ReleaseDate: new DateTime(2024, 6, 15),
            CoverArtId: "cover",
            IsAvailable: true);

        ReleaseInfo? release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.NotNull(release);
        Assert.EndsWith("[FLAC] [WEB]", release.Title);  // Lossless, not [AAC] [96] [WEB]
        Assert.Contains(":Lossless", release.Guid);
    }

    [Fact]
    public void ConvertToReleaseInfoStatic_EmptyAlbumId_ReturnsNull()
    {
        TidalAlbumInfo album = CreateAlbum(id: "");

        ReleaseInfo? release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);

        Assert.Null(release);
    }

    [Fact]
    public void ConvertToReleaseInfosStatic_UsesActualTrackDurations_WhenAvailable()
    {
        // Albums with tracks should use actual duration data for size estimation,
        // not the hardcoded 240s average
        List<TidalTrackInfo> tracks =
        [
            new("t1", "Short", ["A"], "al1", "Album", 1, 60, TidalQuality.Lossless, true, DateTime.Now),
            new("t2", "Short2", ["A"], "al1", "Album", 2, 60, TidalQuality.Lossless, true, DateTime.Now),
        ];

        TidalAlbumInfo albumWithTracks = new(
            Id: "al1",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: tracks,
            AvailableQualities: new List<TidalQuality> { TidalQuality.Lossless },
            ReleaseDate: new DateTime(2024, 6, 15),
            CoverArtId: "cover",
            IsAvailable: true);

        TidalAlbumInfo albumNoTracks = new(
            Id: "al1",
            Title: "Album",
            Artists: new List<string> { "Artist" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality> { TidalQuality.Lossless },
            ReleaseDate: new DateTime(2024, 6, 15),
            CoverArtId: "cover",
            IsAvailable: true);

        List<ReleaseInfo> releasesWithTracks = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(albumWithTracks)];
        List<ReleaseInfo> releasesNoTracks = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(albumNoTracks)];

        // Find the Lossless release in each set
        ReleaseInfo withTrack = releasesWithTracks.First(r => r.Guid.Contains(":Lossless"));
        ReleaseInfo noTrack = releasesNoTracks.First(r => r.Guid.Contains(":Lossless"));

        // 2 tracks * 60s = 120s actual vs 12 tracks * 240s = 2880s default
        // Album with tracks should have a smaller estimated size
        Assert.True(withTrack.Size < noTrack.Size,
            $"Album with 2 short tracks ({withTrack.Size}) should be smaller than default estimate ({noTrack.Size})");
    }
}
