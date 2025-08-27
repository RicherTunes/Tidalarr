using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Domain.Quality;
using Tidalarr.Domain.Streaming;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration;

public class TidalDownloadClient
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
    {
        _streamService = streamService;
        _chunkDownloader = chunkDownloader;
        _apiClient = apiClient;
        _qualityDetector = qualityDetector;
        _settings = settings;
    }
    
    public async Task<TidalDownloadResult> DownloadTrackAsync(string trackId, TidalQuality? quality = null)
    {
        try
        {
            // Get track info
            var track = await _apiClient.GetTrackAsync(trackId);
            
            // Determine quality to download
            var preferredQuality = quality ?? ParsePreferredQuality(_settings.PreferredQuality);
            
            // Get stream info
            var streamInfo = await _streamService.GetStreamInfoAsync(trackId, preferredQuality);
            
            // Download and assemble chunks
            var progress = new Progress<int>(chunksComplete => 
            {
                // TODO: Report progress to Lidarr
            });
            
            using var audioStream = await _chunkDownloader.DownloadAndAssembleAsync(streamInfo, progress);
            
            // ARCHITECT FIX: Stream to disk for large files
            var estimatedSize = EstimateDownloadSize(track.Duration, preferredQuality);
            string outputPath = GetTempFilePath(track, streamInfo.FileExtension);
            
            if (estimatedSize > 50_000_000) // 50MB threshold
            {
                await StreamToFileAsync(audioStream, outputPath);
                var audioData = new byte[0]; // Empty array for large files
            }
            else
            {
                // Small files can still use memory
                var audioData = new byte[audioStream.Length];
                await audioStream.ReadAsync(audioData);
                await File.WriteAllBytesAsync(outputPath, audioData);
            }
            
            return new TidalDownloadResult
            {
                TrackId = trackId,
                Title = track.Title,
                Artist = string.Join(", ", track.Artists),
                Quality = preferredQuality,
                FileExtension = streamInfo.FileExtension,
                AudioData = new byte[0], // File path based now
                FilePath = outputPath,
                Success = true,
                ErrorMessage = string.Empty
            };
        }
        catch (Exception ex)
        {
            return new TidalDownloadResult
            {
                TrackId = trackId,
                Success = false,
                ErrorMessage = $"Download failed: {ex.Message}"
            };
        }
    }
    
    public async Task<List<TidalDownloadResult>> DownloadAlbumAsync(string albumId)
    {
        try
        {
            var album = await _apiClient.GetAlbumAsync(albumId);
            var results = new List<TidalDownloadResult>();
            
            // Download each track in the album
            foreach (var track in album.Tracks)
            {
                var result = await DownloadTrackAsync(track.Id);
                results.Add(result);
                
                // Brief pause between tracks to be respectful
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            
            return results;
        }
        catch (Exception ex)
        {
            return new List<TidalDownloadResult>
            {
                new TidalDownloadResult
                {
                    TrackId = albumId,
                    Success = false,
                    ErrorMessage = $"Album download failed: {ex.Message}"
                }
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
    
    // ARCHITECT FIX: Helper methods for memory management
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
        safeName = string.Join("_", safeName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(Path.GetTempPath(), $"tidalarr_{safeName}{extension}");
    }
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
