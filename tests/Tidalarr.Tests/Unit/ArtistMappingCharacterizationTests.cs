using System.Text.Json;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Characterization tests to lock down the artist mapping behavior.
/// Search results only have 'artists' array, while album details have both 'artist' and 'artists'.
/// </summary>
public class ArtistMappingCharacterizationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void SearchResult_OnlyArtistsArray_DeserializesCorrectly()
    {
        // Tidal search results only include 'artists' array, not singular 'artist'
        const string searchResultJson = """
        {
            "id": 123456789,
            "title": "Test Album",
            "artists": [
                { "id": 111, "name": "Primary Artist" },
                { "id": 222, "name": "Featured Artist" }
            ],
            "audioQuality": "LOSSLESS",
            "releaseDate": "2024-01-15",
            "cover": "abc123",
            "streamReady": true,
            "numberOfTracks": 10
        }
        """;

        var album = JsonSerializer.Deserialize<TidalAlbumDto>(searchResultJson, JsonOptions);

        Assert.NotNull(album);
        Assert.Equal(123456789L, album.id);
        Assert.Null(album.artist);
        Assert.NotNull(album.artists);
        Assert.Equal(2, album.artists.Count);
        Assert.Equal("Primary Artist", album.artists[0].name);
        Assert.Equal("Featured Artist", album.artists[1].name);
    }

    [Fact]
    public void AlbumDetail_BothArtistAndArtistsArray_DeserializesCorrectly()
    {
        // Tidal album details include both singular 'artist' and 'artists' array
        const string albumDetailJson = """
        {
            "id": 987654321,
            "title": "Detailed Album",
            "artist": { "id": 111, "name": "Main Artist" },
            "artists": [
                { "id": 111, "name": "Main Artist" },
                { "id": 333, "name": "Collaborator" }
            ],
            "audioQuality": "HI_RES_LOSSLESS",
            "releaseDate": "2024-06-20",
            "cover": "xyz789",
            "streamReady": true,
            "numberOfTracks": 15
        }
        """;

        var album = JsonSerializer.Deserialize<TidalAlbumDto>(albumDetailJson, JsonOptions);

        Assert.NotNull(album);
        Assert.Equal(987654321L, album.id);
        Assert.NotNull(album.artist);
        Assert.Equal("Main Artist", album.artist.name);
        Assert.NotNull(album.artists);
        Assert.Equal(2, album.artists.Count);
    }

    [Fact]
    public void IdAsString_FlexibleLongConverterHandlesIt()
    {
        // Some Tidal endpoints/markets return IDs as strings
        const string stringIdJson = """
        {
            "id": "123456789",
            "title": "String ID Album",
            "audioQuality": "LOSSLESS",
            "streamReady": true,
            "numberOfTracks": 5
        }
        """;

        var album = JsonSerializer.Deserialize<TidalAlbumDto>(stringIdJson, JsonOptions);

        Assert.NotNull(album);
        Assert.Equal(123456789L, album.id);
    }

    [Fact]
    public void IdAsNumber_StandardDeserialization()
    {
        // Standard case: IDs as numbers
        const string numberIdJson = """
        {
            "id": 987654321,
            "name": "Test Artist"
        }
        """;

        var artist = JsonSerializer.Deserialize<TidalArtistDto>(numberIdJson, JsonOptions);

        Assert.NotNull(artist);
        Assert.Equal(987654321L, artist.id);
        Assert.Equal("Test Artist", artist.name);
    }

    [Fact]
    public void Track_WithNestedAlbumAndArtists_DeserializesCorrectly()
    {
        const string trackJson = """
        {
            "id": 555555555,
            "title": "Test Track",
            "artists": [
                { "id": 111, "name": "Track Artist" }
            ],
            "album": {
                "id": 444444444,
                "title": "Parent Album",
                "artists": [
                    { "id": 111, "name": "Track Artist" }
                ]
            },
            "trackNumber": 3,
            "duration": 240,
            "audioQuality": "LOSSLESS",
            "streamReady": true
        }
        """;

        var track = JsonSerializer.Deserialize<TidalTrackDto>(trackJson, JsonOptions);

        Assert.NotNull(track);
        Assert.Equal(555555555L, track.id);
        Assert.NotNull(track.artists);
        Assert.Single(track.artists);
        Assert.NotNull(track.album);
        Assert.Equal(444444444L, track.album.id);
    }
}
