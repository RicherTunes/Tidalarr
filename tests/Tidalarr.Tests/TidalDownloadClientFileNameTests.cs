using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Utilities;
using System.Collections.Generic;
using System.Text;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalDownloadClientFileNameTests
{
    private class ExposedDownloadClient : TidalDownloadClient
    {
        public ExposedDownloadClient()
            : base(new TidalStreamService(new CoreStub(), new TidalManifestParser()),
                   new TidalChunkDownloader(new HttpClient()),
                   new CoreStub(),
                   new TidalQualityDetector(),
                   new TidalDownloadClientSettings())
        { }

        public string ExposeGenerateFileName(StreamingTrack track, StreamingAlbum album)
        {
            return base.GenerateFileName(track, album);
        }
    }

    private class CoreStub : Core.Interfaces.ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));
        }

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
        }

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<TidalTrackInfo>());
        }

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
        {
            return GetAlbumAsync(albumId, cancellationToken);
        }

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TidalSearchResults([], [], [], 0, false));
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
    [InlineData("  T  I  T  L  E  ")]
    [InlineData("Cafe\u0301")]
    public void GenerateFileName_ShouldMatchCommonTrackFileNameContract(string title)
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new() { Artist = new StreamingArtist { Name = "Art:ist" } };
        StreamingTrack track = new() { Title = title, Artist = new StreamingArtist { Name = "Art:ist" }, TrackNumber = 0 };

        string fileName = client.ExposeGenerateFileName(track, album);
        string expected = FileSystemUtilities.CreateTrackFileName(title, 0, "flac", 1, 1);

        Assert.Equal(expected, fileName);
        Assert.EndsWith(".flac", fileName);
        Assert.Contains(" - ", fileName);
        Assert.DoesNotContain("Art", fileName, StringComparison.Ordinal);

        // Ensure illegal characters are removed
        char[] illegalChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
        Assert.DoesNotContain(illegalChars, fileName.Contains);

        // Ensure reserved names are not used verbatim as the final component
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string lastComponent = baseName.Split(" - ").Last();
        Assert.False(string.Equals("CON", lastComponent, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenerateFileName_ZeroPadsTrackNumber()
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new() { Artist = new StreamingArtist { Name = "Artist" } };
        StreamingTrack track = new() { Title = "Title", Artist = new StreamingArtist { Name = "Artist" }, TrackNumber = 3 };

        string fileName = client.ExposeGenerateFileName(track, album);
        Assert.StartsWith("03 - ", fileName);
    }

    [Fact]
    public void GenerateFileName_WithDiscNumberGreaterThanOne_IncludesDiscPrefix()
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new()
        {
            Artist = new StreamingArtist { Name = "Artist" },
            Metadata = new Dictionary<string, object> { [StreamingMetadataKeys.TotalDiscs] = 2 }
        };
        StreamingTrack track = new()
        {
            Title = "Title",
            Artist = new StreamingArtist { Name = "Artist" },
            DiscNumber = 2,
            TrackNumber = 3
        };

        string fileName = client.ExposeGenerateFileName(track, album);
        Assert.StartsWith("D02T03 - ", fileName);
    }

    [Fact]
    public void GenerateFileName_NormalizesUnicodeToFormC()
    {
        ExposedDownloadClient client = new();
        StreamingAlbum album = new() { Artist = new StreamingArtist { Name = "Cafe\u0301 Artist" } };
        StreamingTrack track = new()
        {
            Title = "Cafe\u0301 Title",
            Artist = new StreamingArtist { Name = "Cafe\u0301 Artist" },
            TrackNumber = 1
        };

        string fileName = client.ExposeGenerateFileName(track, album);
        string expected = FileSystemUtilities.CreateTrackFileName("Cafe\u0301 Title", 1, "flac", 1, 1);

        Assert.Equal(expected, fileName);
        Assert.True(fileName.IsNormalized(NormalizationForm.FormC));
        Assert.Contains("Café", fileName, StringComparison.Ordinal);
    }
}

