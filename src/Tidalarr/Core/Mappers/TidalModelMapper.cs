using System;
using System.Collections.Generic;
using System.Linq;
using Lidarr.Plugin.Common.Models;
using Tidalarr.Core.Models;

namespace Tidalarr.Core.Mappers;

/// <summary>
/// Maps between Tidal-specific models and shared library streaming models
/// </summary>
public class TidalModelMapper
{
    /// <summary>
    /// Maps TidalTrackInfo to StreamingTrack
    /// </summary>
    public StreamingTrack ToStreamingTrack(TidalTrackInfo track)
    {
        if (track == null) return null;

        return new StreamingTrack
        {
            Id = track.Id,
            Title = track.Title,
            Artist = new StreamingArtist 
            { 
                Id = track.Artists?.FirstOrDefault(),
                Name = string.Join(", ", track.Artists ?? new List<string>()) 
            },
            Album = track.Album != null ? ToStreamingAlbum(track.Album) : null,
            TrackNumber = track.TrackNumber,
            DiscNumber = track.DiscNumber,
            Duration = track.Duration,
            IsExplicit = track.IsExplicit,
            Isrc = track.Isrc,
            FeaturedArtists = new List<StreamingArtist>(),
            AvailableQualities = new List<StreamingQuality> { ToStreamingQuality(track.Quality) },
            PreviewUrl = track.PreviewUrl,
            Popularity = track.Popularity,
            Metadata = new Dictionary<string, object>
            {
                ["tidal_id"] = track.Id,
                ["tidal_url"] = track.Url,
                ["copyright"] = track.Copyright,
                ["version"] = track.Version
            }
        };
    }

    /// <summary>
    /// Maps TidalAlbumInfo to StreamingAlbum
    /// </summary>
    public StreamingAlbum ToStreamingAlbum(TidalAlbumInfo album)
    {
        if (album == null) return null;

        return new StreamingAlbum
        {
            Id = album.Id,
            Title = album.Title,
            Artist = new StreamingArtist 
            { 
                Id = album.ArtistId,
                Name = string.Join(", ", album.Artists ?? new List<string>()) 
            },
            AdditionalArtists = new List<StreamingArtist>(),
            ReleaseDate = album.ReleaseDate,
            Type = MapAlbumType(album.Type),
            TrackCount = album.TrackCount,
            Duration = TimeSpan.Zero, // Not available in TidalAlbumInfo
            Genres = new List<string>(), // Not available in TidalAlbumInfo
            Label = string.Empty, // Not available in TidalAlbumInfo
            Upc = string.Empty, // Not available in TidalAlbumInfo
            AvailableQualities = album.AvailableQualities?.Select(ToStreamingQuality).ToList() ?? new List<StreamingQuality>(),
            CoverArtUrls = new Dictionary<string, string>
            {
                ["small"] = album.CoverArtId,
                ["medium"] = album.CoverArtId,
                ["large"] = album.CoverArtId,
                ["original"] = album.CoverArtId
            },
            ExternalUrls = new Dictionary<string, string>
            {
                ["tidal"] = $"https://tidal.com/browse/album/{album.Id}"
            },
            Metadata = new Dictionary<string, object>
            {
                ["tidal_id"] = album.Id,
                ["cover_art_id"] = album.CoverArtId,
                ["available_qualities"] = string.Join(",", album.AvailableQualities),
                ["is_available"] = album.IsAvailable,
                ["release_date"] = album.ReleaseDate
            }
        };
    }

    /// <summary>
    /// Maps TidalArtistInfo to StreamingArtist (if we have this model)
    /// </summary>
    public StreamingArtist ToStreamingArtist(string artistId, string artistName, Dictionary<string, object> metadata = null)
    {
        return new StreamingArtist
        {
            Id = artistId,
            Name = artistName,
            Biography = string.Empty,
            Genres = new List<string>(),
            Country = string.Empty,
            ImageUrls = new Dictionary<string, string>(),
            ExternalUrls = new Dictionary<string, string>(),
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Maps TidalQuality to StreamingQuality
    /// </summary>
    public StreamingQuality ToStreamingQuality(TidalQuality quality)
    {
        return quality switch
        {
            TidalQuality.Low => new StreamingQuality
            {
                Id = "LOW",
                Name = "Normal",
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
                Name = "HiFi",
                Format = "FLAC",
                Bitrate = 1411,
                SampleRate = 44100,
                BitDepth = 16
            },
            TidalQuality.HiRes => new StreamingQuality
            {
                Id = "HI_RES",
                Name = "Master",
                Format = "FLAC",
                Bitrate = null, // Variable for MQA
                SampleRate = 96000, // Common hi-res sample rate
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

    /// <summary>
    /// Maps StreamingQuality back to TidalQuality
    /// </summary>
    public TidalQuality FromStreamingQuality(StreamingQuality quality)
    {
        if (quality == null) return TidalQuality.High;

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

    /// <summary>
    /// Maps Tidal album type to streaming album type
    /// </summary>
    private static StreamingAlbumType MapAlbumType(string tidalType)
    {
        return tidalType?.ToUpperInvariant() switch
        {
            "ALBUM" => StreamingAlbumType.Album,
            "SINGLE" => StreamingAlbumType.Single,
            "EP" => StreamingAlbumType.EP,
            "COMPILATION" => StreamingAlbumType.Compilation,
            "SOUNDTRACK" => StreamingAlbumType.Soundtrack,
            "LIVE" => StreamingAlbumType.Live,
            _ => StreamingAlbumType.Album
        };
    }

    /// <summary>
    /// Creates a list of StreamingTrack from TidalAlbumInfo tracks
    /// </summary>
    public List<StreamingTrack> ToStreamingTracks(TidalAlbumInfo album)
    {
        if (album?.Tracks == null) return new List<StreamingTrack>();

        var streamingAlbum = ToStreamingAlbum(album);
        return album.Tracks.Select(track => 
        {
            var streamingTrack = ToStreamingTrack(track);
            streamingTrack.Album = streamingAlbum; // Set album reference
            return streamingTrack;
        }).ToList();
    }

    /// <summary>
    /// Maps TidalSearchResults to StreamingSearchResult list
    /// </summary>
    public List<StreamingSearchResult> ToStreamingSearchResults(TidalSearchResults searchResults)
    {
        var results = new List<StreamingSearchResult>();

        // Add albums
        if (searchResults?.Albums != null)
        {
            results.AddRange(searchResults.Albums.Select(album => new StreamingSearchResult
            {
                Id = album.Id,
                Title = album.Title,
                Artist = string.Join(", ", album.Artists ?? new List<string>()),
                Album = album.Title,
                Type = StreamingSearchType.Album,
                ReleaseDate = album.ReleaseDate,
                Genre = album.Genres?.FirstOrDefault(),
                Label = album.Label,
                CoverArtUrl = album.CoverArt?.Replace("{w}x{h}", "320x320"),
                TrackCount = album.TrackCount,
                Duration = album.Duration,
                Metadata = new Dictionary<string, object>
                {
                    ["tidal_id"] = album.Id,
                    ["tidal_type"] = "album"
                }
            }));
        }

        // Add tracks
        if (searchResults?.Tracks != null)
        {
            results.AddRange(searchResults.Tracks.Select(track => new StreamingSearchResult
            {
                Id = track.Id,
                Title = track.Title,
                Artist = string.Join(", ", track.Artists ?? new List<string>()),
                Album = track.Album?.Title,
                Type = StreamingSearchType.Track,
                ReleaseDate = track.Album?.ReleaseDate,
                CoverArtUrl = track.Album?.CoverArt?.Replace("{w}x{h}", "320x320"),
                TrackCount = 1,
                Duration = track.Duration,
                Metadata = new Dictionary<string, object>
                {
                    ["tidal_id"] = track.Id,
                    ["tidal_type"] = "track"
                }
            }));
        }

        return results;
    }
}