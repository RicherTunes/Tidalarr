using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Services.Download;
using Lidarr.Plugin.Common.Services.Performance;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration;

public class TidalDownloadClient : BaseDownloadOrchestrator<TidalTrackInfo, TidalAlbumInfo, TidalSettings>
{
    private readonly TidalStreamService _streamService;
    private readonly TidalChunkDownloader _chunkDownloader;
    private readonly ITidalCore _apiClient;
    private readonly TidalQualityDetector _qualityDetector;
    private readonly TidalSettings _settings;
    
    public TidalDownloadClient(
        TidalStreamService streamService,
        TidalChunkDownloader chunkDownloader,
        ITidalCore apiClient,
        TidalQualityDetector qualityDetector,
        TidalSettings settings)
        : base("Tidal", maxConcurrentDownloads: 3) // 3 concurrent downloads max
    {
        _streamService = streamService;
        _chunkDownloader = chunkDownloader;
        _apiClient = apiClient;
        _qualityDetector = qualityDetector;
        _settings = settings;
    }
    
    // Public API methods
    public async Task<TidalDownloadResult> DownloadTrackAsync(string trackId, TidalQuality? quality = null)
    {
        var track = await _apiClient.GetTrackAsync(trackId);
        var trackData = await DownloadTrackDataAsync(track, _settings, CancellationToken.None);
        
        var preferredQuality = quality ?? ParsePreferredQuality(_settings.PreferredQuality);
        var outputPath = GetTempFilePath(track, ".flac"); // Default extension
        await File.WriteAllBytesAsync(outputPath, trackData);
        
        return new TidalDownloadResult
        {
            TrackId = trackId,
            Title = track.Title,
            Artist = string.Join(", ", track.Artists),
            Quality = preferredQuality,
            FileExtension = ".flac",
            AudioData = trackData,
            FilePath = outputPath,
            Success = true,
            ErrorMessage = string.Empty
        };
    }
    
    // Implement required abstract methods from BaseDownloadOrchestrator
    protected override async Task<List<TidalTrackInfo>> GetAlbumTracksAsync(TidalAlbumInfo album, TidalSettings settings)
    {
        // If album doesn't have tracks loaded, fetch them
        if (album.Tracks == null || !album.Tracks.Any())
        {
            var fullAlbum = await _apiClient.GetAlbumAsync(album.Id);
            return fullAlbum.Tracks.ToList();
        }
        return album.Tracks.ToList();
    }
    
    protected override async Task<byte[]> DownloadTrackDataAsync(TidalTrackInfo track, TidalSettings settings, CancellationToken cancellationToken)
    {
        var preferredQuality = ParsePreferredQuality(settings.PreferredQuality);
        var streamInfo = await _streamService.GetStreamInfoAsync(track.Id, preferredQuality);
        
        var progress = new Progress<int>();
        using var audioStream = await _chunkDownloader.DownloadAndAssembleAsync(streamInfo, progress);
        
        var audioData = new byte[audioStream.Length];
        await audioStream.ReadAsync(audioData, cancellationToken);
        return audioData;
    }
    
    protected override string GenerateTrackFileName(TidalTrackInfo track, TidalAlbumInfo album = null, TidalSettings settings = null)
    {
        var albumTitle = album?.Title ?? "Unknown Album";
        var artistName = track.Artists?.FirstOrDefault() ?? "Unknown Artist";
        var trackTitle = track.Title ?? "Unknown Track";
        
        // Sanitize filename
        var fileName = $"{artistName} - {albumTitle} - {track.TrackNumber:D2} - {trackTitle}.flac";
        return SanitizeFileName(fileName);
    }
    
    protected override string GetTrackTitle(TidalTrackInfo track)
    {
        return track.Title ?? "Unknown Track";
    }
    
    protected override string GetAlbumTitle(TidalAlbumInfo album)
    {
        return album.Title ?? "Unknown Album";
    }
    
    // Use BaseDownloadOrchestrator for efficient album downloads
    public async Task<List<TidalDownloadResult>> DownloadAlbumAsync(
        string albumId, 
        string outputDirectory = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var album = await _apiClient.GetAlbumAsync(albumId);
        var outputDir = outputDirectory ?? Path.GetTempPath();
        
        var albumResult = await DownloadAlbumAsync(album, _settings, outputDir, progress, cancellationToken);
        
        return albumResult.TrackResults.Select(tr => new TidalDownloadResult
        {
            TrackId = "unknown", // TrackDownloadResult doesn't contain track ID
            Title = tr.TrackTitle,
            Artist = "unknown", // Would need to extract from track title or metadata
            Quality = ParsePreferredQuality(_settings.PreferredQuality),
            FileExtension = Path.GetExtension(tr.OutputPath),
            AudioData = tr.Success ? File.ReadAllBytes(tr.OutputPath) : Array.Empty<byte>(),
            FilePath = tr.OutputPath,
            Success = tr.Success,
            ErrorMessage = tr.ErrorMessage
        }).ToList();
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
    
    // Helper methods
    private static async Task StreamToFileAsync(Stream sourceStream, string outputPath)
    {
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        await sourceStream.CopyToAsync(fileStream);
        await fileStream.FlushAsync();
    }
    
    private static long EstimateDownloadSize(int durationSeconds, TidalQuality quality)
    {
        var bitrate = quality switch
        {
            TidalQuality.Low => 96_000,
            TidalQuality.High => 320_000,
            TidalQuality.Lossless => 1_411_000,
            TidalQuality.HiRes => 2_304_000,
            _ => 320_000
        };
        
        return (long)(durationSeconds * bitrate / 8);
    }
    
    private static string GetTempFilePath(TidalTrackInfo track, string extension)
    {
        var safeName = $"{string.Join(", ", track.Artists)} - {track.Title}";
        safeName = SanitizeFileName(safeName);
        return Path.Combine(Path.GetTempPath(), $"tidalarr_{safeName}{extension}");
    }
    
    // SanitizeFileName method is inherited from BaseDownloadOrchestrator
}

public class TidalDownloadResult
{
    public string TrackId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public TidalQuality Quality { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public string FilePath { get; set; } = string.Empty; // ARCHITECT FIX: File path for large files
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
