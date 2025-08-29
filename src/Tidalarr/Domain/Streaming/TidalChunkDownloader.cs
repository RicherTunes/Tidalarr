using System.Text.Json;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class ChunkDownloadProgress
{
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; }
    public double ProgressPercentage => TotalChunks > 0 ? (double)CompletedChunks / TotalChunks * 100 : 0;
}

public class TidalChunkDownloader
{
    private readonly HttpClient _httpClient;
    
    public TidalChunkDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    /// <summary>
    /// Download and assemble chunks from Tidal DASH manifest
    /// </summary>
    public async Task<MemoryStream> DownloadAndAssembleAsync(
        StreamManifest manifest, 
        IProgress<ChunkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputStream = new MemoryStream();
        var totalChunks = manifest.ChunkUrls.Length;
        var completedChunks = 0;
        
        foreach (var chunkUrl in manifest.ChunkUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                var response = await _httpClient.GetAsync(chunkUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var chunkData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                await outputStream.WriteAsync(chunkData, 0, chunkData.Length, cancellationToken);
                
                completedChunks++;
                progress?.Report(new ChunkDownloadProgress 
                { 
                    TotalChunks = totalChunks, 
                    CompletedChunks = completedChunks 
                });
                
                // Small delay to avoid overwhelming Tidal's servers
                await Task.Delay(50, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to download chunk {chunkUrl}: {ex.Message}", ex);
            }
        }
        
        outputStream.Position = 0;
        return outputStream;
    }
    
    /// <summary>
    /// Legacy method for backward compatibility with existing TidalStreamInfo
    /// </summary>
    public async Task<Stream> DownloadAndAssembleAsync(TidalStreamInfo streamInfo, IProgress<int>? progress = null)
    {
        // ARCHITECT FIX: Stream directly to temp file for memory efficiency
        var tempFilePath = Path.GetTempFileName();
        var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.DeleteOnClose);
        
        try
        {
            // CRITICAL: Tidal chunks MUST be downloaded sequentially to preserve order
            for (int i = 0; i < streamInfo.ChunkUrls.Length; i++)
            {
                var chunkUrl = streamInfo.ChunkUrls[i];
                
                // Stream chunk directly to file (no memory loading)
                using var response = await _httpClient.GetAsync(chunkUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                using var contentStream = await response.Content.ReadAsStreamAsync();
                await contentStream.CopyToAsync(fileStream);
                
                // Report progress
                progress?.Report(i + 1);
            }
            
            fileStream.Seek(0, SeekOrigin.Begin);
            return fileStream;
        }
        catch
        {
            fileStream?.Dispose();
            throw;
        }
    }
    
    public async Task<byte[]> DownloadAndAssembleBytesAsync(TidalStreamInfo streamInfo, IProgress<int>? progress = null)
    {
        using var stream = await DownloadAndAssembleAsync(streamInfo, progress);
        return ((MemoryStream)stream).ToArray();
    }
    
    private async Task<byte[]> DownloadChunkWithRetryAsync(string chunkUrl, int maxRetries = 3)
    {
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await _httpClient.GetByteArrayAsync(chunkUrl);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                lastException = ex;
                
                // Brief delay before retry
                await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)));
            }
        }
        
        throw new InvalidOperationException($"Failed to download chunk {chunkUrl} after {maxRetries} attempts", lastException);
    }
    
    public async Task<bool> ValidateChunkAccessibilityAsync(string[] chunkUrls)
    {
        try
        {
            // Test first chunk to validate accessibility
            if (chunkUrls.Length == 0)
                return false;
                
            using var response = await _httpClient.GetAsync(chunkUrls[0], HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Validate chunk accessibility for new StreamManifest format
    /// </summary>
    public async Task<bool> ValidateChunkAccessibilityAsync(StreamManifest manifest)
    {
        return await ValidateChunkAccessibilityAsync(manifest.ChunkUrls);
    }
}
