using System.Net;
using System.Text;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests;

public class TidalChunkDownloaderTests
{
    [Fact]
    public async Task DownloadAndAssemble_ValidUrls_ReturnsAssembledStream()
    {
        // Arrange
        var testData = new[] { "chunk1data", "chunk2data", "chunk3data" };
        var httpClient = CreateMockHttpClientWithChunks(testData);
        var downloader = new TidalChunkDownloader(httpClient);
        
        var streamInfo = new TidalStreamInfo(
            TrackId: "123",
            ChunkUrls: new[] { "https://test.com/1", "https://test.com/2", "https://test.com/3" },
            FileExtension: ".flac",
            MimeType: "application/dash+xml",
            IsEncrypted: false,
            SecurityToken: null
        );
        
        // Act
        using var result = await downloader.DownloadAndAssembleAsync(streamInfo);
        
        // Assert
        Assert.NotNull(result);
        
        // Verify chunks are assembled in order
        using var ms = new MemoryStream();
        await result.CopyToAsync(ms);
        var assembledContent = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal("chunk1datachunk2datachunk3data", assembledContent);
    }
    
    [Fact]
    public async Task DownloadAndAssembleBytes_ValidUrls_ReturnsBytes()
    {
        // Arrange
        var testData = new[] { "test", "data" };
        var httpClient = CreateMockHttpClientWithChunks(testData);
        var downloader = new TidalChunkDownloader(httpClient);
        
        var streamInfo = new TidalStreamInfo(
            "123", new[] { "https://test.com/1", "https://test.com/2" },
            ".flac", "application/dash+xml", false, null);
        
        // Act
        var result = await downloader.DownloadAndAssembleBytesAsync(streamInfo);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("testdata", Encoding.UTF8.GetString(result));
    }
    
    [Fact]
    public async Task ValidateChunkAccessibility_ValidUrl_ReturnsTrue()
    {
        // Arrange
        var httpClient = CreateMockHttpClientWithChunks(new[] { "test" });
        var downloader = new TidalChunkDownloader(httpClient);
        
        // Act
        var result = await downloader.ValidateChunkAccessibilityAsync(new[] { "https://test.com/valid" });
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task ValidateChunkAccessibility_EmptyUrls_ReturnsFalse()
    {
        // Arrange
        var httpClient = new HttpClient();
        var downloader = new TidalChunkDownloader(httpClient);
        
        // Act
        var result = await downloader.ValidateChunkAccessibilityAsync(Array.Empty<string>());
        
        // Assert
        Assert.False(result);
    }
    
    private static HttpClient CreateMockHttpClientWithChunks(string[] chunks)
    {
        var handler = new MockChunkHttpMessageHandler(chunks);
        return new HttpClient(handler);
    }
}

public class MockChunkHttpMessageHandler : HttpMessageHandler
{
    private readonly string[] _chunks;
    private int _chunkIndex = 0;
    
    public MockChunkHttpMessageHandler(string[] chunks)
    {
        _chunks = chunks;
    }
    
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        // Return chunks in order
        if (_chunkIndex < _chunks.Length)
        {
            var chunkData = Encoding.UTF8.GetBytes(_chunks[_chunkIndex]);
            response.Content = new ByteArrayContent(chunkData);
            _chunkIndex++;
        }
        return Task.FromResult(response);
    }
}
