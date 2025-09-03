using System;
using System.Collections.Generic;
using System.Linq;
using Lidarr.Plugin.Common.Models;
using Tidalarr.Core.Models;

namespace Tidalarr.Core.Mappers;

public class TidalModelMapper
{
    public StreamingTrack ToStreamingTrack(TidalTrackInfo track)
    {
        // Create a non-null StreamingTrack even if inputs are sparse
        var artistName = string.Join(", ", track.Artists ?? new List<string>());
        return new StreamingTrack
        {
            Id = track.Id ?? string.Empty,
            Title = track.Title ?? string.Empty,
            Artist = new StreamingArtist
            {
                Id = (track.Artists?.FirstOrDefault() ?? string.Empty),
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
            FeaturedArtists = new List<StreamingArtist>(),
            AvailableQualities = new List<StreamingQuality> { ToStreamingQuality(track.Quality) },
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
    }

    public StreamingAlbum ToStreamingAlbum(TidalAlbumInfo album)
    {
        var artistName = string.Join(", ", album.Artists ?? new List<string>());
        return new StreamingAlbum
        {
            Id = album.Id ?? string.Empty,
            Title = album.Title ?? string.Empty,
            Artist = new StreamingArtist
            {
                Id = album.Artists?.FirstOrDefault() ?? string.Empty,
                Name = artistName
            },
            AdditionalArtists = new List<StreamingArtist>(),
            ReleaseDate = album.ReleaseDate,
            Type = StreamingAlbumType.Album,
            TrackCount = album.Tracks?.Count ?? 0,
            Duration = TimeSpan.Zero,
            Genres = new List<string>(),
            Label = string.Empty,
            Upc = string.Empty,
            AvailableQualities = (album.AvailableQualities ?? new List<TidalQuality>()).Select(ToStreamingQuality).ToList(),
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
    }

    public StreamingArtist ToStreamingArtist(string artistId, string artistName, Dictionary<string, object>? metadata = null)
        => new()
        {
            Id = artistId ?? string.Empty,
            Name = artistName ?? string.Empty,
            Biography = string.Empty,
            Genres = new List<string>(),
            Country = string.Empty,
            ImageUrls = new Dictionary<string, string>(),
            ExternalUrls = new Dictionary<string, string>(),
            Metadata = metadata ?? new Dictionary<string, object>()
        };

    public StreamingQuality ToStreamingQuality(TidalQuality quality)
    {
        return quality switch
        {
            TidalQuality.Low => new StreamingQuality
            {
                Id = "LOW",
                Name = "Low",
                Format = "AAC",
                Bitrate = 96,
                SampleRate = 44100,
                BitDepth = null
            },
            TidalQuality.High => new StreamingQuality
            {
                Id = "HIGH",
                Name = "High",
                Format = "AAC",
                Bitrate = 320,
                SampleRate = 44100,
                BitDepth = null
            },
            TidalQuality.Lossless => new StreamingQuality
            {
                Id = "LOSSLESS",
                Name = "Lossless",
                Format = "FLAC",
                Bitrate = null,
                SampleRate = 44100,
                BitDepth = 16
            },
            TidalQuality.HiRes => new StreamingQuality
            {
                Id = "HI_RES",
                Name = "Master",
                Format = "FLAC",
                Bitrate = null,
                SampleRate = 96000,
                BitDepth = 24
            },
            _ => new StreamingQuality
            {
                Id = "HIGH",
                Name = "High",
                Format = "AAC",
                Bitrate = 320,
                SampleRate = 44100,
                BitDepth = null
            }
        };
    }

    public TidalQuality FromStreamingQuality(StreamingQuality quality)
    {
        var tier = quality.GetTier();
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
        var streamingAlbum = ToStreamingAlbum(album);
        return (album.Tracks ?? new List<TidalTrackInfo>()).Select(track =>
        {
            var streamingTrack = ToStreamingTrack(track);
            streamingTrack.Album = streamingAlbum;
            return streamingTrack;
        }).ToList();
    }

    public List<StreamingSearchResult> ToStreamingSearchResults(TidalSearchResults searchResults)
    {
        var results = new List<StreamingSearchResult>();

        if (searchResults?.Albums != null)
        {
            results.AddRange(searchResults.Albums.Select(album => new StreamingSearchResult
            {
                Id = album.Id ?? string.Empty,
                Title = album.Title ?? string.Empty,
                Artist = string.Join(", ", album.Artists ?? new List<string>()),
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
                Artist = string.Join(", ", track.Artists ?? new List<string>()),
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
