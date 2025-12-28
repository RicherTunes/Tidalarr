using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Base;
using Lidarr.Plugin.Abstractions.Models;
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
    ILogger? logger = null,
    IAudioFormatHandler? audioFormatHandler = null) : BaseStreamingDownloadClient<TidalDownloadClientSettings>(settings, logger!)
{
    private const string LegacyTotalDiscsMetadataKey = "total_discs";
    private const string LegacyNumberOfVolumesMetadataKey = "number_of_volumes";

    private readonly TidalStreamService _streamService = streamService;
    private readonly TidalChunkDownloader _chunkDownloader = chunkDownloader;   
    private readonly ITidalCore _apiClient = apiClient;
    private readonly TidalQualityDetector _qualityDetector = qualityDetector;   
    private readonly TidalModelMapper _mapper = new();
    private readonly IAudioFormatHandler _audioFormatHandler = audioFormatHandler ?? new DefaultAudioFormatHandler();

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

        const int maxReasonableDiscs = 99;

        int? totalDiscsMetadata = TryGetIntMetadata(album?.Metadata, StreamingMetadataKeys.TotalDiscs)
            ?? TryGetIntMetadata(album?.Metadata, LegacyTotalDiscsMetadataKey);
        if (totalDiscsMetadata is < 1 or > maxReasonableDiscs) totalDiscsMetadata = null;

        int? numberOfVolumesMetadata = TryGetIntMetadata(album?.Metadata, LegacyNumberOfVolumesMetadataKey);
        if (numberOfVolumesMetadata is < 1 or > maxReasonableDiscs) numberOfVolumesMetadata = null;

        int totalDiscs = totalDiscsMetadata
            ?? numberOfVolumesMetadata
            ?? 1;
        totalDiscs = Math.Max(1, totalDiscs);
        totalDiscs = Math.Max(totalDiscs, discNumber);

        string title = track.Title ?? "Unknown Track";
        string extension = Settings.ExtractFlac ? "flac" : "m4a";

        return FileSystemUtilities.CreateTrackFileName(title, trackNumber, extension, discNumber, totalDiscs);
    }

    private static int? TryGetIntMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null) return null;
        if (!metadata.TryGetValue(key, out object? value)) return null;
        if (value == null) return null;

        if (value is int i) return i;
        if (value is long l)
        {
            if (l > int.MaxValue || l < int.MinValue) return null;
            return (int)l;
        }
        if (value is short s) return s;
        if (value is byte b) return b;
        if (value is float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f)) return null;
            if (f > int.MaxValue || f < int.MinValue) return null;
            var rounded = (int)Math.Round(f);
            return Math.Abs(f - rounded) < 0.0001f ? rounded : null;
        }
        if (value is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return null;
            if (d > int.MaxValue || d < int.MinValue) return null;
            var rounded = (int)Math.Round(d);
            return Math.Abs(d - rounded) < 0.0000001 ? rounded : null;
        }
        if (value is decimal dec)
        {
            if (dec > int.MaxValue || dec < int.MinValue) return null;
            if (dec != decimal.Truncate(dec)) return null;
            return (int)dec;
        }
        if (value is string str)
        {
            if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
            {
                return parsedInt;
            }

            const NumberStyles decimalStyles =
                NumberStyles.AllowLeadingWhite |
                NumberStyles.AllowTrailingWhite |
                NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint;

            if (decimal.TryParse(str, decimalStyles, CultureInfo.InvariantCulture, out decimal parsedDecimal))
            {
                if (parsedDecimal > int.MaxValue || parsedDecimal < int.MinValue) return null;
                if (parsedDecimal != decimal.Truncate(parsedDecimal)) return null;
                return (int)parsedDecimal;
            }
        }

        return null;
    }

    private static string NormalizeDotExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        string trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }

    private static bool PathsEqual(string path1, string path2)
    {
        string full1 = Path.GetFullPath(path1);
        string full2 = Path.GetFullPath(path2);

        return string.Equals(
            full1,
            full2,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static void MoveFileOverwrite(string sourcePath, string destinationPath)
    {
        try
        {
            File.Move(sourcePath, destinationPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(sourcePath, destinationPath);
        }
    }

    private static string ResolveTempOutputPath(string outputPath, string? tempExtension)
    {
        string normalizedTempExt = NormalizeDotExtension(tempExtension);
        if (string.IsNullOrEmpty(normalizedTempExt))
        {
            return outputPath;
        }

        if (string.IsNullOrEmpty(Path.GetExtension(outputPath)))
        {
            return outputPath + normalizedTempExt;
        }

        return Path.ChangeExtension(outputPath, normalizedTempExt);
    }

    protected async Task<string> FinalizeDownloadedTrackAsync(
        Stream audioStream,
        string outputPath,
        string? payloadFileExtension,
        string? mimeType,
        StreamingTrack track,
        CancellationToken cancellationToken,
        string? tempFileExtension = null,
        bool extractFlac = false,
        string? codec = null)
    {
        string tempOutputPath = ResolveTempOutputPath(outputPath, tempFileExtension);
        string partialPath = tempOutputPath + ".partial";

        string directory = Path.GetDirectoryName(tempOutputPath) ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);

        if (File.Exists(partialPath))
        {
            try { File.Delete(partialPath); } catch { /* ignore */ }
        }

        await using (FileStream fileStream = new(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
        {
            byte[] header = new byte[512];
            int read = await audioStream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new InvalidDataException("Downloaded stream contained no data.");
            }

            DownloadPayloadValidator.ValidateOrThrow(header.AsSpan(0, read), payloadFileExtension, mimeType);

            await fileStream.WriteAsync(header.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            await audioStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            try { fileStream.Flush(true); } catch { /* best effort */ }
        }

        MoveFileOverwrite(partialPath, tempOutputPath);

        string processedPath = tempOutputPath;
        if (extractFlac)
        {
            if (!string.Equals(codec, "FLAC", StringComparison.OrdinalIgnoreCase))
            {
                Logger?.LogWarning("ExtractFlac is enabled but codec is '{Codec}'. File will be renamed to match outputPath.", codec);
            }
            else
            {
                processedPath = await _audioFormatHandler.ProcessAudioFileAsync(
                        tempOutputPath,
                        codec,
                        extractFlac: true,
                        keepOriginal: false)
                    .ConfigureAwait(false);
            }
        }

        if (!PathsEqual(processedPath, outputPath))
        {
            MoveFileOverwrite(processedPath, outputPath);
            processedPath = outputPath;
        }

        DownloadPayloadValidator.ValidateFileOrThrow(processedPath);
        await ApplyMetadataTagsAsync(processedPath, track).ConfigureAwait(false);

        return processedPath;
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

            await using Stream audioStream = await this._chunkDownloader
                .DownloadAndAssembleStreamAsync(manifest, progress, cancellationToken)
                .ConfigureAwait(false);
            string finalPath = await FinalizeDownloadedTrackAsync(        
                    audioStream,
                    outputPath,
                    manifest.FileExtension,
                    manifest.MimeType,
                    track,
                    cancellationToken,
                    tempFileExtension: manifest.FileExtension,
                    extractFlac: Settings.ExtractFlac,
                    codec: manifest.Codec)
                .ConfigureAwait(false);

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

    private async Task<JsonElement> GetStreamManifestDataAsync(string trackId, TidalQuality quality)
    {
        TidalStreamInfo streamInfo = await this._apiClient.GetStreamInfoAsync(trackId, quality);

        // Create JsonElement from stream info for StreamManifest constructor
        JsonElement manifestJson = JsonSerializer.SerializeToElement(new
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
            StreamingTrack track = await GetTrackAsync(trackId);
            TidalQuality quality = preferredQuality ?? Settings.PreferredQuality;
            TidalStreamInfo streamInfo = await this._streamService.GetStreamInfoAsync(trackId, quality);

            string dir2 = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath();
            _ = Directory.CreateDirectory(dir2);

            Progress<int> progress = new();
            await using Stream audioStream = await this._chunkDownloader
                .DownloadAndAssembleAsync(streamInfo, progress, cancellationToken)
                .ConfigureAwait(false);

            string finalPath = await FinalizeDownloadedTrackAsync(        
                    audioStream,
                    outputPath,
                    streamInfo.FileExtension,
                    streamInfo.MimeType,
                    track,
                    cancellationToken)
                .ConfigureAwait(false);

            return new StreamingDownloadResult
            {
                Success = true,
                TrackId = trackId,
                OutputPath = finalPath,
                Track = track,
                Quality = this._mapper.ToStreamingQuality(quality),
                FileSize = new FileInfo(finalPath).Length
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

    // Legacy support methods
    public async Task<TidalDownloadResult> DownloadTrackAsync(string trackId, TidalQuality? quality = null)
    {
        TidalTrackInfo track = await this._apiClient.GetTrackAsync(trackId);
        TidalQuality preferredQuality = quality ?? Settings.PreferredQuality;
        string extension = Settings.ExtractFlac ? ".flac" : ".m4a";
        string outputPath = GetTempFilePath(track, extension);

        EnhancedDownloadResult result = await DownloadTrackEnhancedAsync(trackId, outputPath, preferredQuality);

        return new TidalDownloadResult
        {
            TrackId = trackId,
            Title = track.Title,
            Artist = string.Join(", ", track.Artists ?? []),
            Quality = preferredQuality,
            FileExtension = Path.GetExtension(result.OutputPath),
            AudioData = result.Success ? File.ReadAllBytes(result.OutputPath) : [],
            FilePath = result.OutputPath,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage
        };
    }

    private static string GetTempFilePath(TidalTrackInfo track, string extension)
    {
        string safeName = $"{string.Join(", ", track.Artists ?? [])} - {track.Title}";
        safeName = FileSystemUtilities.SanitizeFileName(safeName.Normalize(NormalizationForm.FormC));
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
