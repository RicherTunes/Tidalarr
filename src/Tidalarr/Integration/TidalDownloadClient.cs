using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Base;
using Lidarr.Plugin.Common.Models;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration;

public class TidalDownloadClient : BaseStreamingDownloadClient<TidalSettings>
{
    private readonly TidalStreamService _streamService;
    private readonly TidalChunkDownloader _chunkDownloader;
    private readonly ITidalCore _apiClient;
    private readonly TidalQualityDetector _qualityDetector;
    private readonly TidalModelMapper _mapper;
    
    protected override string ServiceName => "Tidal";
    protected override string ProtocolName => "tidal";
    
    public TidalDownloadClient(
        TidalStreamService streamService,
        TidalChunkDownloader chunkDownloader,
        ITidalCore apiClient,
        TidalQualityDetector qualityDetector,
        TidalSettings settings,
        ILogger logger = null)
        : base(settings, logger)
    {
        _streamService = streamService;
        _chunkDownloader = chunkDownloader;
        _apiClient = apiClient;
        _qualityDetector = qualityDetector;
        _mapper = new TidalModelMapper();
    }
    
    // Implement required abstract methods from BaseStreamingDownloadClient
    protected override async Task<bool> AuthenticateAsync()
    {
        try
        {
            return await _apiClient.IsAuthenticatedAsync();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Tidal authentication failed");
            return false;
        }
    }
    
    protected override async Task<StreamingAlbum> GetAlbumAsync(string albumId)
    {
        var tidalAlbum = await _apiClient.GetAlbumWithTracksAsync(albumId);
        var streamingAlbum = _mapper.ToStreamingAlbum(tidalAlbum);
        
        // Ensure tracks are populated in the streaming album
        if (tidalAlbum.Tracks?.Any() == true)
        {
            var streamingTracks = _mapper.ToStreamingTracks(tidalAlbum);
            // The mapper should handle track-to-album relationships
        }
        
        return streamingAlbum;
    }
    
    protected override async Task<StreamingTrack> GetTrackAsync(string trackId)
    {
        var tidalTrack = await _apiClient.GetTrackAsync(trackId);
        return _mapper.ToStreamingTrack(tidalTrack);
    }
    
    protected override async Task<string> GetStreamUrlAsync(string trackId, string quality)
    {
        var tidalQuality = ParseQualityFromString(quality);
        var streamInfo = await _streamService.GetStreamInfoAsync(trackId, tidalQuality);
        return streamInfo.ChunkUrls?.FirstOrDefault() ?? streamInfo.Url;
    }
    
    protected override ValidationResult ValidateDownloadSettings(TidalSettings settings)
    {
        var result = new ValidationResult();
        
        if (!settings.IsValid(out var errorMessage))
        {
            result.Errors.Add(new FluentValidation.Results.ValidationFailure("Settings", errorMessage));
        }
        
        return result;
    }
    
    protected override string GenerateFileName(StreamingTrack track, StreamingAlbum album)
    {
        var trackNumber = track.TrackNumber?.ToString("D2") ?? "00";
        var title = FileNameSanitizer.SanitizeFileName(track.Title ?? "Unknown Track");
        var artist = FileNameSanitizer.SanitizeFileName(track.Artist?.Name ?? album?.Artist?.Name ?? "Unknown Artist");
        
        return $"{trackNumber} - {artist} - {title}.flac";
    }
    
    /// <summary>
    /// Download a track with enhanced metadata and chunked streaming support
    /// </summary>
    public async Task<StreamingDownloadResult> DownloadTrackWithMetadataAsync(
        string trackId, 
        string outputPath,
        TidalQuality? preferredQuality = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var track = await GetTrackAsync(trackId);
            var quality = preferredQuality ?? ParsePreferredQuality(Settings.PreferredQuality);
            var streamInfo = await _streamService.GetStreamInfoAsync(trackId, quality);
            
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            
            var progress = new Progress<int>();
            using var audioStream = await _chunkDownloader.DownloadAndAssembleAsync(streamInfo, progress);
            
            await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            await audioStream.CopyToAsync(fileStream, cancellationToken);
            
            return new StreamingDownloadResult
            {
                Success = true,
                TrackId = trackId,
                OutputPath = outputPath,
                Track = track,
                Quality = _mapper.ToStreamingQuality(quality),
                FileSize = new FileInfo(outputPath).Length
            };
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, $"Failed to download track {trackId}");
            return new StreamingDownloadResult
            {
                Success = false,
                TrackId = trackId,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> ValidateDownloadAsync(string trackId, TidalQuality quality)
    {
        try
        {
            var streamInfo = await _streamService.GetStreamInfoAsync(trackId, quality);
            return await _chunkDownloader.ValidateChunkAccessibilityAsync(streamInfo.ChunkUrls);
        }
        catch
        {
            return false;
        }
    }
    
    private static TidalQuality ParsePreferredQuality(string? qualityString)
    {
        return qualityString?.ToLowerInvariant() switch
        {
            "low" => TidalQuality.Low,
            "high" => TidalQuality.High,
            "lossless" => TidalQuality.Lossless,
            "hires" => TidalQuality.HiRes,
            _ => TidalQuality.Lossless
        };
    }
    
    private static TidalQuality ParseQualityFromString(string quality)
    {
        return quality?.ToUpperInvariant() switch
        {
            "LOW" => TidalQuality.Low,
            "HIGH" => TidalQuality.High,
            "LOSSLESS" => TidalQuality.Lossless,
            "HI_RES" => TidalQuality.HiRes,
            _ => TidalQuality.Lossless
        };
    }
    
    // Legacy support methods
    public async Task<TidalDownloadResult> DownloadTrackAsync(string trackId, TidalQuality? quality = null)
    {
        var track = await _apiClient.GetTrackAsync(trackId);
        var preferredQuality = quality ?? ParsePreferredQuality(Settings.PreferredQuality);
        var outputPath = GetTempFilePath(track, ".flac");
        
        var result = await DownloadTrackWithMetadataAsync(trackId, outputPath, preferredQuality);
        
        return new TidalDownloadResult
        {
            TrackId = trackId,
            Title = track.Title,
            Artist = string.Join(", ", track.Artists ?? new List<string>()),
            Quality = preferredQuality,
            FileExtension = ".flac",
            AudioData = result.Success ? File.ReadAllBytes(outputPath) : Array.Empty<byte>(),
            FilePath = outputPath,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage
        };
    }
    
    private static string GetTempFilePath(TidalTrackInfo track, string extension)
    {
        var safeName = $"{string.Join(", ", track.Artists ?? new List<string>())} - {track.Title}";
        safeName = FileNameSanitizer.SanitizeFileName(safeName);
        return Path.Combine(Path.GetTempPath(), $"tidalarr_{safeName}{extension}");
    }
}

/// <summary>
/// Legacy TidalDownloadResult for backward compatibility
/// </summary>
public class TidalDownloadResult
{
    public string TrackId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public TidalQuality Quality { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Enhanced download result using shared library models
/// </summary>
public class StreamingDownloadResult
{
    public bool Success { get; set; }
    public string TrackId { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public StreamingTrack Track { get; set; }
    public StreamingQuality Quality { get; set; }
    public long FileSize { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}