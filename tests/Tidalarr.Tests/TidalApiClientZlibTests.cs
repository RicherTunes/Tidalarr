using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Moq;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

/// <summary>
/// Zlib decompression coverage tests for TidalApiClient.
/// Source: src/Tidalarr/Domain/Api/TidalApiClient.cs
/// </summary>
public class TidalApiClientZlibTests
{
    #region LooksLikeZlib 0xDA Second Byte Variant (line 505)

    [Fact]
    public async Task GetTrackAsync_ZlibWithDeflateHeader_DecompressesSuccessfully()
    {
        // Arrange - Test line 505: zlib with deflate encoding header
        // Proof: grep -n "0xDA" src/Tidalarr/Domain/Api/TidalApiClient.cs
        // Line 505: return bytes.Count >= 2 && bytes[0] == 0x78 && (bytes[1] == 0x01 || bytes[1] == 0x9C || bytes[1] == 0xDA);
        string json = JsonSerializer.Serialize(new
        {
            id = 1L,
            title = "Zlib Track",
            artist = new { name = "Artist", id = 1 },
            album = new { id = "al1", title = "Album", releaseDate = "2020-01-01" },
            trackNumber = 1,
            duration = 180,
            streamReady = true,
            audioQuality = "LOSSLESS"
        });

        // Compress with zlib (DeflateStream produces raw deflate without the 0x78 zlib header;
        // use ZLibStream so we exercise the LooksLikeZlib(0x78 ..) detection path on line 505).
        using MemoryStream ms = new();
        using (ZLibStream zlib = new(ms, CompressionLevel.Optimal, leaveOpen: true))
        using (StreamWriter writer = new(zlib, Encoding.UTF8))
        {
            writer.Write(json);
        }
        byte[] compressedBytes = ms.ToArray();

        // Verify zlib header (0x78 prefix)
        Assert.True(compressedBytes.Length >= 2);
        Assert.Equal(0x78, compressedBytes[0]);

        DeflateHandler handler = new(compressedBytes);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = new();
        mockAuth.Setup(a => a.GetValidTokensAsync())
            .ReturnsAsync(new TidalTokens("access_token", "refresh_token", "Bearer",
                DateTime.UtcNow.AddHours(1), "session123", "US", "user123"));
        TidalApiClient client = new(httpClient, mockAuth.Object);

        // Act
        TidalTrackInfo result = await client.GetTrackAsync("1");

        // Assert
        Assert.Equal("Zlib Track", result.Title);
    }

    #endregion

    #region Helper Methods

    private static Mock<ITidalAuth> CreateMockAuth()
    {
        Mock<ITidalAuth> mock = new();
        mock.Setup(a => a.GetValidTokensAsync())
            .ReturnsAsync(new TidalTokens("access_token", "refresh_token", "Bearer",
                DateTime.UtcNow.AddHours(1), "session123", "US", "user123"));
        return mock;
    }

    #endregion

    #region Mock Handlers

    private class DeflateHandler(byte[] content) : HttpMessageHandler
    {
        private readonly byte[] _content = content;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ByteArrayContent byteContent = new(_content);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            byteContent.Headers.ContentEncoding.Add("deflate");

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = byteContent
            };
            return Task.FromResult(response);
        }
    }

    #endregion
}
