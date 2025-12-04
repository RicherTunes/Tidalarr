using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Core.Models;

namespace Tidalarr.Core.Mappers;

public class TidalModelMapper
{
    public StreamingTrack ToStreamingTrack(TidalTrackInfo track)
    {
        // Create a non-null StreamingTrack even if inputs are sparse
        string artistName = string.Join(", ", track.Artists ?? []);
        StreamingTrack streaming = new StreamingTrack
        {
            Id = track.Id ?? string.Empty,
            Title = track.Title ?? string.Empty,
            Artist = new StreamingArtist
            {
                Id = track.Artists?.FirstOrDefault() ?? string.Empty,
                Name = artistName
            },
            Album = new StreamingAlbum
            {
                Id = track.AlbumId ?? string.Empty,
                Title = track.AlbumTitle ?? string.Empty,
                Artist = new StreamingArtist
                {
                    Id = track.Artists?.FirstOrDefault() ?? string.Empty,
                    Name = artistName
                }
            },
            TrackNumber = track.TrackNumber,
            DiscNumber = 1,
            Duration = TimeSpan.FromSeconds(track.Duration),
            IsExplicit = false,
            Isrc = string.Empty,
            FeaturedArtists = [],
            AvailableQualities = [ToStreamingQuality(track.Quality)],
            PreviewUrl = string.Empty,
            Popularity = 0,
            Metadata = new Dictionary<string, object>
            {
                ["tidal_id"] = track.Id ?? string.Empty,
                ["album_id"] = track.AlbumId ?? string.Empty,
                ["release_date"] = track.ReleaseDate,
                ["is_available"] = track.IsAvailable
            }
        };

        // Fill external IDs
        streaming.ExternalIds["tidal"] = streaming.Id;
        // MusicBrainz id not available from DTOs; leave default unless provided elsewhere
        return streaming;
    }

    public StreamingAlbum ToStreamingAlbum(TidalAlbumInfo album)
    {
        string artistName = string.Join(", ", album.Artists ?? []);
        StreamingAlbum streaming = new StreamingAlbum
        {
            Id = album.Id ?? string.Empty,
            Title = album.Title ?? string.Empty,
            Artist = new StreamingArtist
            {
                Id = album.Artists?.FirstOrDefault() ?? string.Empty,
                Name = artistName
            },
            AdditionalArtists = [],
            ReleaseDate = album.ReleaseDate,
            Type = StreamingAlbumType.Album,
            TrackCount = album.Tracks?.Count ?? 0,
            Duration = TimeSpan.Zero,
            Genres = [],
            Label = string.Empty,
            Upc = string.Empty,
            AvailableQualities = [.. (album.AvailableQualities ?? []).Select(ToStreamingQuality)],
            CoverArtUrls = new Dictionary<string, string>
            {
                ["small"] = album.CoverArtId ?? string.Empty,
                ["medium"] = album.CoverArtId ?? string.Empty,
                ["large"] = album.CoverArtId ?? string.Empty,
                ["original"] = album.CoverArtId ?? string.Empty
            },
            ExternalUrls = new Dictionary<string, string>
            {
                ["tidal"] = $"https://tidal.com/browse/album/{album.Id}"
            },
            Metadata = new Dictionary<string, object>
            {
                ["tidal_id"] = album.Id ?? string.Empty,
                ["cover_art_id"] = album.CoverArtId ?? string.Empty,
                ["available_qualities"] = album.AvailableQualities != null ? string.Join(",", album.AvailableQualities) : string.Empty,
                ["is_available"] = album.IsAvailable,
                ["release_date"] = album.ReleaseDate
            }
        };

        // Fill external IDs
        streaming.ExternalIds["tidal"] = streaming.Id;
        // MusicBrainz id unknown here
        return streaming;
    }

    public StreamingArtist ToStreamingArtist(string artistId, string artistName, Dictionary<string, object>? metadata = null)
    {
        return new()
        {
            Id = artistId ?? string.Empty,
            Name = artistName ?? string.Empty,
            Biography = string.Empty,
            Genres = [],
            Country = string.Empty,
            ImageUrls = [],
            ExternalUrls = [],
            Metadata = metadata ?? []
        };
    }

    public StreamingQuality ToStreamingQuality(TidalQuality quality)
    {
        // Preserve Tidal ids for compatibility, populate specs to align with universal tiers
        return quality switch
        {
            TidalQuality.Low => new StreamingQuality { Id = "LOW", Name = "Low", Format = "AAC", Bitrate = 96, SampleRate = 44100 },
            TidalQuality.High => new StreamingQuality { Id = "HIGH", Name = "High", Format = "AAC", Bitrate = 320, SampleRate = 44100 },
            TidalQuality.Lossless => new StreamingQuality { Id = "LOSSLESS", Name = "Lossless", Format = "FLAC", BitDepth = 16, SampleRate = 44100 },
            TidalQuality.HiRes => new StreamingQuality { Id = "HI_RES", Name = "Master", Format = "FLAC", BitDepth = 24, SampleRate = 96000 },
            _ => new StreamingQuality { Id = "HIGH", Name = "High", Format = "AAC", Bitrate = 320, SampleRate = 44100 }
        };
    }

    public TidalQuality FromStreamingQuality(StreamingQuality quality)
    {
        StreamingQualityTier tier = quality.GetTier();
        return tier switch
        {
            StreamingQualityTier.Low => TidalQuality.Low,
            StreamingQualityTier.Normal => TidalQuality.High,
            StreamingQualityTier.High => TidalQuality.High,
            StreamingQualityTier.Lossless => TidalQuality.Lossless,
            StreamingQualityTier.HiRes => TidalQuality.HiRes,
            _ => TidalQuality.High
        };
    }

    public List<StreamingTrack> ToStreamingTracks(TidalAlbumInfo album)
    {
        StreamingAlbum streamingAlbum = ToStreamingAlbum(album);
        return [.. (album.Tracks ?? []).Select(track =>
        {
            StreamingTrack streamingTrack = ToStreamingTrack(track);
            streamingTrack.Album = streamingAlbum;
            return streamingTrack;
        })];
    }

    public List<StreamingSearchResult> ToStreamingSearchResults(TidalSearchResults searchResults)
    {
        List<StreamingSearchResult> results = [];

        if (searchResults?.Albums != null)
        {
            results.AddRange(searchResults.Albums.Select(album => new StreamingSearchResult
            {
                Id = album.Id ?? string.Empty,
                Title = album.Title ?? string.Empty,
                Artist = string.Join(", ", album.Artists ?? []),
                Album = album.Title ?? string.Empty,
                Type = StreamingSearchType.Album,
                ReleaseDate = album.ReleaseDate,
                Genre = string.Empty,
                Label = string.Empty,
                CoverArtUrl = !string.IsNullOrEmpty(album.CoverArtId) ? $"https://resources.tidal.com/images/{album.CoverArtId.Replace("-", "/")}/320x320.jpg" : string.Empty,
                TrackCount = album.Tracks?.Count,
                Duration = TimeSpan.FromSeconds(album.Tracks?.Sum(t => t.Duration) ?? 0),
                Metadata = new Dictionary<string, object>
                {
                    ["tidal_id"] = album.Id ?? string.Empty,
                    ["tidal_type"] = "album"
                }
            }));
        }

        if (searchResults?.Tracks != null)
        {
            results.AddRange(searchResults.Tracks.Select(track => new StreamingSearchResult
            {
                Id = track.Id ?? string.Empty,
                Title = track.Title ?? string.Empty,
                Artist = string.Join(", ", track.Artists ?? []),
                Album = track.AlbumTitle ?? string.Empty,
                Type = StreamingSearchType.Track,
                ReleaseDate = track.ReleaseDate,
                CoverArtUrl = string.Empty,
                TrackCount = 1,
                Duration = TimeSpan.FromSeconds(track.Duration),
                Metadata = new Dictionary<string, object>
                {
                    ["tidal_id"] = track.Id ?? string.Empty,
                    ["tidal_type"] = "track"
                }
            }));
        }

        return results;
    }
}

