using System.Text;
using System.Globalization;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Base;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Security;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Integration;

public class TidalDownloadClient(
    TidalStreamService streamService,
    TidalChunkDownloader chunkDownloader,
    ITidalCore apiClient,
    TidalQualityDetector qualityDetector,
    TidalDownloadClientSettings settings,
    ILogger? logger = null) : BaseStreamingDownloadClient<TidalDownloadClientSettings>(settings, logger!)
{
    private readonly TidalStreamService _streamService = streamService;
    private readonly TidalChunkDownloader _chunkDownloader = chunkDownloader;
    private readonly ITidalCore _apiClient = apiClient;
    private readonly TidalQualityDetector _qualityDetector = qualityDetector;
    private readonly TidalModelMapper _mapper = new();

    protected override string ServiceName => "Tidal";
    protected override string ProtocolName => "tidal";

    // Implement required abstract methods from BaseStreamingDownloadClient
    protected override async Task<bool> AuthenticateAsync()
    {
        try
        {
            return await this._apiClient.IsAuthenticatedAsync();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Tidal authentication failed");
            return false;
        }
    }

    protected override async Task<StreamingAlbum> GetAlbumAsync(string albumId)
    {
        TidalAlbumInfo tidalAlbum = await this._apiClient.GetAlbumWithTracksAsync(albumId);
        StreamingAlbum streamingAlbum = this._mapper.ToStreamingAlbum(tidalAlbum)!;

        // Ensure tracks are populated in the streaming album
        if (tidalAlbum.Tracks?.Any() == true)
        {
            _ = this._mapper.ToStreamingTracks(tidalAlbum);
            // The mapper should handle track-to-album relationships
        }

        return streamingAlbum;
    }

    protected override async Task<StreamingTrack> GetTrackAsync(string trackId)
    {
        TidalTrackInfo tidalTrack = await this._apiClient.GetTrackAsync(trackId);
        return this._mapper.ToStreamingTrack(tidalTrack)!;
    }

    protected override async Task<string> GetStreamUrlAsync(string trackId, string quality)
    {
        TidalQuality tidalQuality = ParseQualityFromString(quality);
        TidalStreamInfo streamInfo = await this._streamService.GetStreamInfoAsync(trackId, tidalQuality);
        return streamInfo.ChunkUrls?.FirstOrDefault() ?? string.Empty;
    }

    protected override ValidationResult ValidateDownloadSettings(TidalDownloadClientSettings settings)
    {
        ValidationResult result = new();

        if (!Enum.IsDefined(typeof(TidalQuality), settings.PreferredQuality))
        {
            result.Errors.Add(new ValidationFailure("PreferredQuality", "Preferred quality selection is invalid"));
        }

        if (string.IsNullOrEmpty(settings.DownloadPath))
        {
            result.Errors.Add(new ValidationFailure("DownloadPath", "Download path is required"));
        }

        return result;
    }

    protected override string GenerateFileName(StreamingTrack track, StreamingAlbum album)
    {
        int trackNumber = track.TrackNumber ?? 0;
        int discNumber = track.DiscNumber.GetValueOrDefault();
        discNumber = discNumber > 0 ? discNumber : 1;
        int totalDiscs = ResolveTotalDiscs(album, discNumber);
        string extension = Settings.ExtractFlac ? "flac" : "m4a";

        return FileSystemUtilities.CreateTrackFileName(
            title: track.Title ?? "Unknown Track",
            trackNumber: trackNumber,
            extension: extension,
            discNumber: discNumber,
            totalDiscs: totalDiscs);
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
            StreamingTrack track = await GetTrackAsync(trackId);
            TidalQuality quality = preferredQuality ?? Settings.PreferredQuality;

            // Step 2: Prefer parsed manifest for accurate chunks and codec within M4A
            TidalManifest manifest = await this._streamService.GetParsedManifestAsync(trackId, quality);

            Logger?.LogInformation($"Downloading track {trackId}: {manifest.Codec} in {manifest.FileExtension} ({manifest.ChunkUrls.Length} chunks)");

            // Step 4: Download and assemble chunks
            string dir = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath();
            _ = Directory.CreateDirectory(dir);

            Progress<ChunkDownloadProgress> progress = new(p =>
            {
                Logger?.LogDebug($"Download progress: {p.CompletedChunks}/{p.TotalChunks} chunks ({p.ProgressPercentage:F1}%)");
            });

            using MemoryStream audioStream = await this._chunkDownloader.DownloadAndAssembleAsync(manifest, Settings.DownloadDelay, progress, cancellationToken);

            // Step 5: Save assembled audio with correct extension
            string tempPath = outputPath + manifest.FileExtension;
            audioStream.Position = 0;
            byte[] header = new byte[512];
            int read = await audioStream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            if (read <= 0)
            {
                throw new InvalidDataException("Downloaded stream contained no data.");
            }

            TidalDownloadPayloadValidator.ValidateOrThrow(header.AsSpan(0, read), manifest.FileExtension, manifest.MimeType);

            audioStream.Position = 0;
            await using (FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                await audioStream.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
                try { fileStream.Flush(true); } catch { /* best effort */ }
            }

            // Step 6: Process audio format (extract FLAC from M4A if needed)
            string finalPath = tempPath;
            if (Settings.ExtractFlac && manifest.Codec == "FLAC")
            {
                string extractedPath = await TidalAudioFormatHandler.ProcessAudioFileAsync(
                    tempPath, manifest.Codec, extractFlac: true, keepOriginal: false);
                finalPath = extractedPath;
            }

            // Rename to final output path if needed
            if (finalPath != outputPath)
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                File.Move(finalPath, outputPath);
                finalPath = outputPath;
            }

            return new EnhancedDownloadResult
            {
                Success = true,
                TrackId = trackId,
                OutputPath = finalPath,
                Track = track,
                Quality = this._mapper.ToStreamingQuality(quality),
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
            StreamingTrack track = await GetTrackAsync(trackId);
            TidalQuality quality = preferredQuality ?? Settings.PreferredQuality;
            TidalStreamInfo streamInfo = await this._streamService.GetStreamInfoAsync(trackId, quality);

            string dir2 = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath();
            _ = Directory.CreateDirectory(dir2);

            // Write to temp .partial for atomicity
            string tempPath = outputPath + ".partial";
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }

            Progress<int> progress = new();
            int maxChunks = Settings.GetEffectiveMaxConcurrentChunkDownloads();
            using Stream audioStream = await this._chunkDownloader.DownloadAndAssembleAsync(
                streamInfo,
                Settings.DownloadDelay,
                maxConcurrentChunkDownloads: maxChunks,
                progress: progress,
                cancellationToken: cancellationToken);

            await using (FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                byte[] header = new byte[512];
                int read = await audioStream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
                if (read <= 0)
                {
                    throw new InvalidDataException("Downloaded stream contained no data.");
                }

                TidalDownloadPayloadValidator.ValidateOrThrow(header.AsSpan(0, read), streamInfo.FileExtension, streamInfo.MimeType);

                await fileStream.WriteAsync(header.AsMemory(0, read), cancellationToken);
                await audioStream.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
                try { fileStream.Flush(true); } catch { /* best effort */ }
            }

            // Atomic move
            try
            {
                File.Move(tempPath, outputPath, overwrite: true);
            }
            catch
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                File.Move(tempPath, outputPath);
            }

            return new StreamingDownloadResult
            {
                Success = true,
                TrackId = trackId,
                OutputPath = outputPath,
                Track = track,
                Quality = this._mapper.ToStreamingQuality(quality),
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
            TidalStreamInfo streamInfo = await this._streamService.GetStreamInfoAsync(trackId, quality);
            return await this._chunkDownloader.ValidateChunkAccessibilityAsync(streamInfo.ChunkUrls);
        }
        catch
        {
            return false;
        }
    }

    // Diagnostics-based validation result with stable ID codes (Common result)
    internal async Task<Lidarr.Plugin.Abstractions.Results.PluginOperationResult<Dictionary<string, string>>> ValidateDownloadWithDiagnosticsAsync(string trackId, TidalQuality quality)
    {
        const string OK = "DL000";              // Validation OK
        const string CHUNK_UNAVAILABLE = "DL001"; // First chunk not accessible
        const string STREAM_ERROR = "DL100";      // Failed to get stream info

        try
        {
            TidalStreamInfo streamInfo = await this._streamService.GetStreamInfoAsync(trackId, quality);
            bool ok = await this._chunkDownloader.ValidateChunkAccessibilityAsync(streamInfo.ChunkUrls);
            if (!ok)
            {
                Dictionary<string, string> metaFail = new()
                {
                    ["id"] = CHUNK_UNAVAILABLE,
                    ["trackId"] = trackId,
                    ["quality"] = quality.ToString(),
                    ["firstChunk"] = streamInfo.ChunkUrls.FirstOrDefault() ?? string.Empty
                };
                return Lidarr.Plugin.Abstractions.Results.PluginOperationResult<Dictionary<string, string>>.Failure(
                    new Lidarr.Plugin.Abstractions.Results.PluginError(
                        Lidarr.Plugin.Abstractions.Results.PluginErrorCode.ProviderUnavailable,
                        "First chunk is not accessible",
                        null,
                        metaFail));
            }

            return Lidarr.Plugin.Abstractions.Results.PluginOperationResult<Dictionary<string, string>>.Success(new()
            {
                ["id"] = OK,
                ["trackId"] = trackId,
                ["quality"] = quality.ToString(),
                ["chunkCount"] = (streamInfo.ChunkUrls?.Length ?? 0).ToString()
            });
        }
        catch (Exception ex)
        {
            Dictionary<string, string> metaErr = new()
            {
                ["id"] = STREAM_ERROR,
                ["trackId"] = trackId,
                ["quality"] = quality.ToString()
            };
            return Lidarr.Plugin.Abstractions.Results.PluginOperationResult<Dictionary<string, string>>.Failure(
                new Lidarr.Plugin.Abstractions.Results.PluginError(
                    Lidarr.Plugin.Abstractions.Results.PluginErrorCode.ProviderUnavailable,
                    ex.Message,
                    ex,
                    metaErr));
        }
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

    private static int ResolveTotalDiscs(StreamingAlbum? album, int discNumber)
    {
        int totalDiscs = 1;
        object? raw = null;

        if (album?.Metadata?.TryGetValue(StreamingMetadataKeys.TotalDiscs, out raw) == true)
        {
            switch (raw)
            {
                case int value when value > 0:
                    totalDiscs = value;
                    break;
                case long value when value > 0 && value <= int.MaxValue:
                    totalDiscs = (int)value;
                    break;
                case string value when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0:
                    totalDiscs = parsed;
                    break;
            }
        }

        return Math.Max(totalDiscs, discNumber);
    }

    // Legacy support methods
    public async Task<TidalDownloadResult> DownloadTrackAsync(string trackId, TidalQuality? quality = null)
    {
        TidalTrackInfo track = await this._apiClient.GetTrackAsync(trackId);
        TidalQuality preferredQuality = quality ?? Settings.PreferredQuality;
        string outputPath = GetTempFilePath(track, ".flac");

        StreamingDownloadResult result = await DownloadTrackWithMetadataAsync(trackId, outputPath, preferredQuality);

        return new TidalDownloadResult
        {
            TrackId = trackId,
            Title = track.Title,
            Artist = string.Join(", ", track.Artists ?? []),
            Quality = preferredQuality,
            FileExtension = ".flac",
            AudioData = result.Success ? File.ReadAllBytes(outputPath) : [],
            FilePath = outputPath,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage
        };
    }

    private static string GetTempFilePath(TidalTrackInfo track, string extension)
    {
        string safeName = $"{string.Join(", ", track.Artists ?? [])} - {track.Title}";
        safeName = Sanitize.FileNameSegment(safeName.Normalize(NormalizationForm.FormC));
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
    public byte[] AudioData { get; set; } = [];
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
