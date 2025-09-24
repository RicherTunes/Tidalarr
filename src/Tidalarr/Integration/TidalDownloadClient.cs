using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
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

public class TidalDownloadClient : BaseStreamingDownloadClient<TidalDownloadSettings>
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
        TidalDownloadSettings settings,
        Microsoft.Extensions.Logging.ILogger? logger = null)
        : base(settings, logger!)
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
        var streamingAlbum = _mapper.ToStreamingAlbum(tidalAlbum)!;
        
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
        return _mapper.ToStreamingTrack(tidalTrack)!;
    }
    
    protected override async Task<string> GetStreamUrlAsync(string trackId, string quality)
    {
        var tidalQuality = ParseQualityFromString(quality);
        var streamInfo = await _streamService.GetStreamInfoAsync(trackId, tidalQuality);
        return streamInfo.ChunkUrls?.FirstOrDefault() ?? string.Empty;
    }
    
    protected override ValidationResult ValidateDownloadSettings(TidalDownloadSettings settings)
    {
        var result = new ValidationResult();
        
        if (string.IsNullOrEmpty(settings.PreferredQuality))
        {
            result.Errors.Add(new FluentValidation.Results.ValidationFailure("PreferredQuality", "Preferred quality is required"));
        }
        
        if (string.IsNullOrEmpty(settings.DownloadPath))
        {
            result.Errors.Add(new FluentValidation.Results.ValidationFailure("DownloadPath", "Download path is required"));
        }
        
        return result;
    }
    
    protected override string GenerateFileName(StreamingTrack track, StreamingAlbum album)
    {
        var trackNumber = track.TrackNumber ?? 0;
        var baseTitle = (track.Title ?? "Unknown Track").Normalize(System.Text.NormalizationForm.FormC);
        var baseArtist = (track.Artist?.Name ?? album?.Artist?.Name ?? "Unknown Artist").Normalize(System.Text.NormalizationForm.FormC);
        var title = Lidarr.Plugin.Common.Utilities.FileSystemUtilities.SanitizeFileName(baseTitle);
        var artist = Lidarr.Plugin.Common.Utilities.FileSystemUtilities.SanitizeFileName(baseArtist);
        var tn = trackNumber > 0 ? trackNumber.ToString("D2") : "00";
        var extension = Settings.ExtractFlac ? "flac" : "m4a";
        return $"{tn} - {artist} - {title}.{extension}";
    }
    
    /// <summary>
    /// Download a track with proper DASH manifest parsing and M4A format handling
    /// </summary>
    public async Task<EnhancedDownloadResult> DownloadTrackEnhancedAsync(
        string trackId, 
        string outputPath,
        TidalQuality? preferredQuality = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Get track metadata
            var track = await GetTrackAsync(trackId);
            var quality = preferredQuality ?? ParsePreferredQuality(Settings.PreferredQuality);
            
            // Step 2: Prefer parsed manifest for accurate chunks and codec within M4A
            var manifest = await _streamService.GetParsedManifestAsync(trackId, quality);
            
            Logger?.LogInformation($"Downloading track {trackId}: {manifest.Codec} in {manifest.FileExtension} ({manifest.ChunkUrls.Length} chunks)");

            Console.WriteLine($"[PreDownload] track {trackId} encrypted={manifest.IsEncrypted} tokenLen={(manifest.SecurityToken?.Length ?? 0)} codec={manifest.Codec}");
            
            // Step 4: Download and assemble chunks
            var dir = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath();
            Directory.CreateDirectory(dir);
            
            var progress = new Progress<ChunkDownloadProgress>(p => 
            {
                Logger?.LogDebug($"Download progress: {p.CompletedChunks}/{p.TotalChunks} chunks ({p.ProgressPercentage:F1}%)");
            });
            
            using var audioStream = await _chunkDownloader.DownloadAndAssembleAsync(manifest, progress, cancellationToken);
            
            // Step 5: Save assembled audio with correct extension
            var tempPath = outputPath + manifest.FileExtension;
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
            audioStream.Position = 0;
            await audioStream.CopyToAsync(fileStream, cancellationToken);
            
            // Step 6: Process audio format (extract FLAC from M4A if needed)
            var finalPath = tempPath;
            if (Settings.ExtractFlac && manifest.Codec == "FLAC")
            {
                var extractedPath = await AudioFormatHandler.ProcessAudioFileAsync(
                    tempPath, manifest.Codec, extractFlac: true, keepOriginal: false);
                finalPath = extractedPath;
            }
            
            // Rename to final output path if needed
            if (finalPath != outputPath)
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                File.Move(finalPath, outputPath);
                finalPath = outputPath;
            }
            
            return new EnhancedDownloadResult
            {
                Success = true,
                TrackId = trackId,
                OutputPath = finalPath,
                Track = track,
                Quality = _mapper.ToStreamingQuality(quality),
                FileSize = new FileInfo(finalPath).Length,
                OriginalFormat = manifest.FileExtension,
                ExtractedFormat = Path.GetExtension(finalPath),
                Codecs = manifest.Codec,
                ChunkCount = manifest.ChunkUrls.Length
            };
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, $"Enhanced download failed for track {trackId}");
            return new EnhancedDownloadResult
            {
                Success = false,
                TrackId = trackId,
                ErrorMessage = ex.Message
            };
        }
    }
    
    private async Task<JsonElement> GetStreamManifestDataAsync(string trackId, TidalQuality quality)
    {
        var streamInfo = await _apiClient.GetStreamInfoAsync(trackId, quality);
        
        // Create JsonElement from stream info for StreamManifest constructor
        var manifestJson = JsonSerializer.SerializeToElement(new
        {
            manifestMimeType = streamInfo.MimeType,
            manifest = "placeholder", // streamInfo doesn't have raw manifest - will be handled differently
            keyId = streamInfo.SecurityToken
        });
        
        return manifestJson;
    }
    
    /// <summary>
    /// Legacy download method with enhanced metadata and chunked streaming support
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
            
            var dir2 = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath();
            Directory.CreateDirectory(dir2);

            // Write to temp .partial for atomicity
            var tempPath = outputPath + ".partial";
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }

            var progress = new Progress<int>();
            using var audioStream = await _chunkDownloader.DownloadAndAssembleAsync(streamInfo, progress);

            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                await audioStream.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
                try { fileStream.Flush(true); } catch { /* best effort */ }
            }

            // Optional: quick container signature validation when we can infer type
            TryValidateSignature(tempPath, streamInfo.FileExtension);

            // Atomic move
            try
            {
                File.Move(tempPath, outputPath, overwrite: true);
            }
            catch
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(tempPath, outputPath);
            }

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

    private void TryValidateSignature(string filePath, string fileExtension)
    {
        try
        {
            var ext = (fileExtension ?? string.Empty).ToLowerInvariant();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[12];
            var read = fs.Read(header);
            if (read < 4) return; // not enough to validate

            if (ext.Contains("flac"))
            {
                // FLAC starts with fLaC
                if (!(header[0] == (byte)'f' && header[1] == (byte)'L' && header[2] == (byte)'a' && header[3] == (byte)'C'))
                {
                    throw new InvalidDataException("Invalid FLAC header signature");
                }
            }
            else if (ext.Contains("m4a") || ext.Contains("mp4"))
            {
                // MP4 variants typically include 'ftyp' box early
                var s = System.Text.Encoding.ASCII.GetString(header.ToArray());
                if (!s.Contains("ftyp"))
                {
                    throw new InvalidDataException("Invalid MP4/M4A header signature");
                }
            }
        }
        catch (Exception ex)
        {
            // Opt-in validation only: log but do not fail hard unless we want strict mode later
            Logger?.LogWarning(ex, "Signature validation warning for {File}", filePath);
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
        safeName = Lidarr.Plugin.Common.Utilities.FileSystemUtilities.SanitizeFileName(safeName.Normalize(System.Text.NormalizationForm.FormC));
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
/// Enhanced download result with DASH manifest and M4A format details
/// </summary>
public class EnhancedDownloadResult
{
    public bool Success { get; set; }
    public string TrackId { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public StreamingTrack? Track { get; set; }
    public StreamingQuality? Quality { get; set; }
    public long FileSize { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    
    // Enhanced properties for Tidal-specific details
    public string OriginalFormat { get; set; } = string.Empty; // e.g., ".m4a"
    public string ExtractedFormat { get; set; } = string.Empty; // e.g., ".flac"
    public string Codecs { get; set; } = string.Empty; // e.g., "FLAC", "MP4A"
    public int ChunkCount { get; set; } // Number of DASH chunks downloaded
    public bool WasExtracted { get; set; } // Whether FLAC was extracted from M4A
}

/// <summary>
/// Enhanced download result using shared library models
/// </summary>
public class StreamingDownloadResult
{
    public bool Success { get; set; }
    public string TrackId { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public StreamingTrack? Track { get; set; }
    public StreamingQuality? Quality { get; set; }
    public long FileSize { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}


