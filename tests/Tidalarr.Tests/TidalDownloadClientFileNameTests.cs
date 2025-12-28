using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalDownloadClientFileNameTests
{
    private const string LegacyNumberOfVolumesMetadataKey = "number_of_volumes";

    private sealed class ExposedDownloadClient : TidalDownloadClient
    {
        public ExposedDownloadClient()
            : base(
                new TidalStreamService(new CoreStub(), new TidalManifestParser()),
                new TidalChunkDownloader(new HttpClient()),
                new CoreStub(),
                new TidalQualityDetector(),
                new TidalDownloadClientSettings())
        {
        }

        public string ExposeGenerateFileName(StreamingTrack track, StreamingAlbum album)
        {
            return base.GenerateFileName(track, album);
        }
    }

    private sealed class CoreStub : Core.Interfaces.ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new TidalTrackInfo("", "", new List<string>(), "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new TidalAlbumInfo("", "", new List<string>(), new List<TidalTrackInfo>(), new List<TidalQuality>(), DateTime.MinValue, "", true));
        }

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<TidalTrackInfo>());
        }

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return GetAlbumAsync(albumId, cancellationToken);
        }

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 1000, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalSearchResults(new List<TidalAlbumInfo>(), new List<TidalTrackInfo>(), new List<TidalArtistInfo>(), 0, false));
        }

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));
        }

        public Task<bool> IsAuthenticatedAsync()
        {
            return Task.FromResult(true);
        }
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("AUX?*|\"")]
    [InlineData("Title. ")]
    public void GenerateFileName_SanitizesReservedAndInvalidCharacters(string title)
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new() { Artist = new StreamingArtist { Name = "Artist" } };
        StreamingTrack track = new() { Title = title, TrackNumber = 1, DiscNumber = 1 };

        string fileName = client.ExposeGenerateFileName(track, album);

        Assert.StartsWith("01 - ", fileName);
        Assert.EndsWith(".flac", fileName);

        char[] illegalChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
        Assert.DoesNotContain(illegalChars, fileName.Contains);

        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string safeTitle = baseName.Split(" - ").Last();

        Assert.False(string.Equals("CON", safeTitle, StringComparison.OrdinalIgnoreCase));
        Assert.False(string.Equals("AUX", safeTitle, StringComparison.OrdinalIgnoreCase));
        Assert.False(safeTitle.EndsWith('.'));
        Assert.False(safeTitle.EndsWith(' '));
    }

    [Fact]
    public void GenerateFileName_ZeroPadsTrackNumber()
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new() { Artist = new StreamingArtist { Name = "Artist" } };
        StreamingTrack track = new() { Title = "Title", TrackNumber = 3, DiscNumber = 1 };

        string fileName = client.ExposeGenerateFileName(track, album);
        Assert.StartsWith("03 - ", fileName);
    }

    [Fact]
    public void GenerateFileName_MultiDisc_PrefixesDiscAndPreventsCollisions()  
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new()
        {
            Artist = new StreamingArtist { Name = "Artist" },
            Metadata = new Dictionary<string, object> { [StreamingMetadataKeys.TotalDiscs] = 2 }
        };

        StreamingTrack disc1 = new()
        {
            Title = "Title",
            DiscNumber = 1,
            TrackNumber = 1
        };

        StreamingTrack disc2 = new()
        {
            Title = "Title",
            DiscNumber = 2,
            TrackNumber = 1
        };

        string fileNameDisc1 = client.ExposeGenerateFileName(disc1, album);
        string fileNameDisc2 = client.ExposeGenerateFileName(disc2, album);

        Assert.StartsWith("D01T01 - ", fileNameDisc1);
        Assert.StartsWith("D02T01 - ", fileNameDisc2);
        Assert.NotEqual(fileNameDisc1, fileNameDisc2);
    }

    [Theory]
    [InlineData("02")]
    [InlineData("2.0")]
    public void GenerateFileName_MultiDisc_ParsesTotalDiscsStringMetadata(string totalDiscsValue)
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new()
        {
            Artist = new StreamingArtist { Name = "Artist" },
            Metadata = new Dictionary<string, object> { [StreamingMetadataKeys.TotalDiscs] = totalDiscsValue }
        };

        StreamingTrack disc1 = new()
        {
            Title = "Title",
            DiscNumber = 1,
            TrackNumber = 1
        };

        string fileName = client.ExposeGenerateFileName(disc1, album);
        Assert.StartsWith("D01T01 - ", fileName);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("1000")]
    [InlineData("1e2")]
    [InlineData("2,0")]
    public void GenerateFileName_InvalidOrOutOfRangeTotalDiscsMetadata_DefaultsToSingleDisc(string totalDiscsValue)
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new()
        {
            Artist = new StreamingArtist { Name = "Artist" },
            Metadata = new Dictionary<string, object> { [StreamingMetadataKeys.TotalDiscs] = totalDiscsValue }
        };

        StreamingTrack disc1 = new()
        {
            Title = "Title",
            DiscNumber = 1,
            TrackNumber = 1
        };

        string fileName = client.ExposeGenerateFileName(disc1, album);
        Assert.StartsWith("01 - ", fileName);
        Assert.DoesNotContain("D01T01 - ", fileName);
    }

    [Fact]
    public void GenerateFileName_InvalidOrOutOfRangeTotalDiscsMetadata_AsNumber_DefaultsToSingleDisc()
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new()
        {
            Artist = new StreamingArtist { Name = "Artist" },
            Metadata = new Dictionary<string, object>
            {
                [StreamingMetadataKeys.TotalDiscs] = double.MaxValue
            }
        };

        StreamingTrack disc1 = new()
        {
            Title = "Title",
            DiscNumber = 1,
            TrackNumber = 1
        };

        string fileName = client.ExposeGenerateFileName(disc1, album);
        Assert.StartsWith("01 - ", fileName);
    }

    [Fact]
    public void CreateTrackFileName_NormalizesLeadingDotExtension()
    {
        string fileName = FileSystemUtilities.CreateTrackFileName("Title", 1, ".m4a", discNumber: 1, totalDiscs: 1);

        Assert.EndsWith(".m4a", fileName);
        Assert.DoesNotContain("..m4a", fileName);
    }

    [Fact]
    public void SanitizeFileName_TrimsTrailingDotAndSpaceAndAvoidsReservedNames()
    {
        Assert.Equal("_CON", FileSystemUtilities.SanitizeFileName("CON. "));
        Assert.Equal("_AUX", FileSystemUtilities.SanitizeFileName("AUX "));
    }

    [Fact]
    public void GenerateFileName_TotalDiscsMetadata_TakesPrecedenceOverLegacyNumberOfVolumes()
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new()
        {
            Artist = new StreamingArtist { Name = "Artist" },
            Metadata = new Dictionary<string, object>
            {
                [StreamingMetadataKeys.TotalDiscs] = 1,
                [LegacyNumberOfVolumesMetadataKey] = 2
            }
        };

        StreamingTrack disc1 = new()
        {
            Title = "Title",
            DiscNumber = 1,
            TrackNumber = 1
        };

        string fileName = client.ExposeGenerateFileName(disc1, album);
        Assert.StartsWith("01 - ", fileName);
    }
}
