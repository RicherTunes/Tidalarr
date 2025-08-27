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
        // CRITICAL: Tidal chunks MUST be downloaded sequentially to preserve order
        var memoryStream = new MemoryStream();
        
        for (int i = 0; i < streamInfo.ChunkUrls.Length; i++)
        {
            var chunkUrl = streamInfo.ChunkUrls[i];
            var chunkData = await DownloadChunkWithRetryAsync(chunkUrl);
            
            await memoryStream.WriteAsync(chunkData);
            
            // Report progress
            progress?.Report(i + 1);
        }
        
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
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
