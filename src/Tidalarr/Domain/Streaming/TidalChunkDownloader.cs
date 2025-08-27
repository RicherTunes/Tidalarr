using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class TidalChunkDownloader
{
    private readonly HttpClient _httpClient;
    
    public TidalChunkDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
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
}
