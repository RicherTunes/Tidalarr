using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Moq;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

/// <summary>
/// Final coverage tests for TidalApiClient - targets remaining uncovered paths.
/// Source: src/Tidalarr/Domain/Api/TidalApiClient.cs
/// </summary>
public class TidalApiClientFinalCovTests
{
    #region Null Response Content (line 450-452)

    /// <summary>
    /// Covers null response.Content handling in ReadContentAsStringAsync.
    /// Proof: grep -n "if (response.Content == null)" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 450: if (response.Content == null) { return string.Empty; }
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_NullResponseContent_ReturnsEmptyStringAndFailsParse()
    {
        NullContentHandler handler = new();
        HttpClient httpClient = new(handler);
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth);

        JsonException ex = await Assert.ThrowsAsync<JsonException>(() => client.GetTrackAsync("1"));
        Assert.NotNull(ex);
    }

    #endregion

    #region HasEncoding Paths Without Magic Bytes (lines 468, 472, 476)

    /// <summary>
    /// Covers HasEncoding(response, "gzip") path when bytes lack magic bytes.
    /// Proof: grep -n "HasEncoding.*gzip" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 468: if (LooksLikeGzip(bytes) || HasEncoding(response, "gzip"))
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_GzipEncodingHeaderWithoutMagicBytes_Decompresses()
    {
        string json = JsonSerializer.Serialize(new
        {
            id = 1L,
            title = "Gzip Via Header",
            artist = new { name = "Artist", id = 1 },
            album = new { id = "al1", title = "Album", releaseDate = "2020-01-01" },
            trackNumber = 1,
            duration = 180,
            streamReady = true,
            audioQuality = "LOSSLESS"
        });

        byte[] compressedBytes = CompressGzip(json);
        EncodingHeaderHandler handler = new(compressedBytes, "gzip");
        HttpClient httpClient = new(handler);
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth);

        TidalTrackInfo result = await client.GetTrackAsync("1");
        Assert.Equal("Gzip Via Header", result.Title);
    }

    /// <summary>
    /// Covers HasEncoding(response, "deflate") path when bytes lack magic bytes.
    /// Proof: grep -n "HasEncoding.*deflate" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 472: if (LooksLikeZlib(bytes) || HasEncoding(response, "deflate"))
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_DeflateEncodingHeaderWithoutMagicBytes_Decompresses()
    {
        string json = JsonSerializer.Serialize(new
        {
            id = 1L,
            title = "Deflate Via Header",
            artist = new { name = "Artist", id = 1 },
            album = new { id = "al1", title = "Album", releaseDate = "2020-01-01" },
            trackNumber = 1,
            duration = 180,
            streamReady = true,
            audioQuality = "LOSSLESS"
        });

        byte[] compressedBytes = CompressDeflate(json);
        EncodingHeaderHandler handler = new(compressedBytes, "deflate");
        HttpClient httpClient = new(handler);
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth);

        TidalTrackInfo result = await client.GetTrackAsync("1");
        Assert.Equal("Deflate Via Header", result.Title);
    }

    /// <summary>
    /// Covers HasEncoding(response, "br") Brotli decompression path.
    /// Proof: grep -n 'HasEncoding.*"br"' src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 476: if (HasEncoding(response, "br"))
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_BrotliEncodingHeader_Decompresses()
    {
        string json = JsonSerializer.Serialize(new
        {
            id = 1L,
            title = "Brotli Via Header",
            artist = new { name = "Artist", id = 1 },
            album = new { id = "al1", title = "Album", releaseDate = "2020-01-01" },
            trackNumber = 1,
            duration = 180,
            streamReady = true,
            audioQuality = "LOSSLESS"
        });

        byte[] compressedBytes = CompressBrotli(json);
        EncodingHeaderHandler handler = new(compressedBytes, "br");
        HttpClient httpClient = new(handler);
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth);

        TidalTrackInfo result = await client.GetTrackAsync("1");
        Assert.Equal("Brotli Via Header", result.Title);
    }

    #endregion

    #region LooksLikeGzip Short Bytes (line 498-500)

    /// <summary>
    /// Covers empty byte array (bytes.Count = 0, less than 2) falling through to UTF-8 decode.
    /// Proof: grep -n "bytes.Count >= 2" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 498: return bytes.Count >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B;
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_EmptyByteArray_FallsBackToUtf8()
    {
        ByteArrayHandler handler = new(Array.Empty<byte>());
        HttpClient httpClient = new(handler);
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth);

        JsonException ex = await Assert.ThrowsAsync<JsonException>(() => client.GetTrackAsync("1"));
        Assert.NotNull(ex);
    }

    /// <summary>
    /// Covers single byte array (bytes.Count = 1, less than 2) falling through to UTF-8 decode.
    /// Proof: grep -n "bytes.Count >= 2" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 498: return bytes.Count >= 2 && ...
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_SingleByteArray_FallsBackToUtf8()
    {
        ByteArrayHandler handler = new([0x1F]);
        HttpClient httpClient = new(handler);
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth);

        JsonException ex = await Assert.ThrowsAsync<JsonException>(() => client.GetTrackAsync("1"));
        Assert.NotNull(ex);
    }

    #endregion

    #region LooksLikeZlib Second Byte Variants (line 503-506)

    /// <summary>
    /// Covers deflate encoding header - decompression via HasEncoding path.
    /// Proof: grep -n "HasEncoding.*deflate" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 472: if (LooksLikeZlib(bytes) || HasEncoding(response, "deflate"))
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_DeflateEncodingHeader_Decompresses()
    {
        string json = JsonSerializer.Serialize(new
        {
            id = 1L,
            title = "Deflate Encoded",
            artist = new { name = "Artist", id = 1 },
            album = new { id = "al1", title = "Album", releaseDate = "2020-01-01" },
            trackNumber = 1,
            duration = 180,
            streamReady = true,
            audioQuality = "LOSSLESS"
        });

        byte[] compressedBytes = CompressDeflate(json);
        Assert.NotEmpty(compressedBytes);

        EncodingHeaderHandler handler = new(compressedBytes, "deflate");
        HttpClient httpClient = new(handler);
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth);

        TidalTrackInfo result = await client.GetTrackAsync("1");
        Assert.Equal("Deflate Encoded", result.Title);
    }

    #endregion

    #region HasEncoding Null Headers (line 510)

    /// <summary>
    /// Covers path when no ContentEncoding header is present.
    /// Proof: grep -n "ContentEncoding?.Any" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 510: response.Content?.Headers?.ContentEncoding?.Any(...) == true
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_NoContentEncodingHeader_ReturnsUtf8()
    {
        string json = JsonSerializer.Serialize(new
        {
            id = 1L,
            title = "No Encoding Header",
            artist = new { name = "Artist", id = 1 },
            album = new { id = "al1", title = "Album", releaseDate = "2020-01-01" },
            trackNumber = 1,
            duration = 180,
            streamReady = true,
            audioQuality = "LOSSLESS"
        });

        BodyHandler handler = new(json);
        HttpClient httpClient = new(handler);
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth);

        TidalTrackInfo result = await client.GetTrackAsync("1");
        Assert.Equal("No Encoding Header", result.Title);
    }

    #endregion

    #region Cache Set Paths (lines 66, 98, 136, 190)

    /// <summary>
    /// Covers cache.Set for track with 1 hour duration.
    /// Proof: grep -n "_cache?.Set.*FromHours(1)" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 66: this._cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(1));
    /// </summary>
    [Fact]
    public async Task GetTrackAsync_CacheSet_StoresWithOneHourDuration()
    {
        string json = JsonSerializer.Serialize(new
        {
            id = 1L,
            title = "Cached Track",
            artist = new { name = "Artist", id = 1 },
            album = new { id = "al1", title = "Album", releaseDate = "2020-01-01" },
            trackNumber = 1,
            duration = 180,
            streamReady = true,
            audioQuality = "LOSSLESS"
        });

        Mock<IStreamingResponseCache> mockCache = new();
        mockCache.Setup(c => c.Get<TidalTrackDto>(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns((TidalTrackDto?)null);
        mockCache.Setup(c => c.ShouldCache(It.IsAny<string>())).Returns(true);

        HttpClient httpClient = new(new BodyHandler(json));
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth, mockCache.Object);

        TidalTrackInfo result = await client.GetTrackAsync("1");

        Assert.Equal("Cached Track", result.Title);
        mockCache.Verify(c => c.Set(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<TidalTrackDto>(),
            TimeSpan.FromHours(1)), Times.Once);
    }

    /// <summary>
    /// Covers cache.Set for album with 2 hour duration.
    /// Proof: grep -n "_cache?.Set.*FromHours(2)" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 98: this._cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(2));
    /// </summary>
    [Fact]
    public async Task GetAlbumAsync_CacheSet_StoresWithTwoHourDuration()
    {
        string json = JsonSerializer.Serialize(new
        {
            id = "al1",
            title = "Cached Album",
            artist = new { name = "Artist", id = 1 },
            releaseDate = "2020-01-01",
            streamReady = true,
            cover = "cover",
            audioQuality = "LOSSLESS"
        });

        Mock<IStreamingResponseCache> mockCache = new();
        mockCache.Setup(c => c.Get<TidalAlbumDto>(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns((TidalAlbumDto?)null);
        mockCache.Setup(c => c.ShouldCache(It.IsAny<string>())).Returns(true);

        HttpClient httpClient = new(new BodyHandler(json));
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth, mockCache.Object);

        TidalAlbumInfo result = await client.GetAlbumAsync("al1");

        Assert.Equal("Cached Album", result.Title);
        mockCache.Verify(c => c.Set(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<TidalAlbumDto>(),
            TimeSpan.FromHours(2)), Times.Once);
    }

    /// <summary>
    /// Covers cache.Set for album tracks with 2 hour duration.
    /// Proof: grep -n "cache?.Set.*dto.*TimeSpan.FromHours(2)" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 136: this._cache?.Set(endpoint, parameters, dto, TimeSpan.FromHours(2));
    /// </summary>
    [Fact]
    public async Task GetAlbumTracksAsync_CacheSet_StoresWithTwoHourDuration()
    {
        string json = JsonSerializer.Serialize(new
        {
            items = new[]
            {
                new { id = 1L, title = "Track 1", artist = new { name = "Artist" }, album = new { id = "al1" }, trackNumber = 1, duration = 180, streamReady = true }
            }
        });

        Mock<IStreamingResponseCache> mockCache = new();
        mockCache.Setup(c => c.Get<TidalAlbumTracksDto>(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns((TidalAlbumTracksDto?)null);
        mockCache.Setup(c => c.ShouldCache(It.IsAny<string>())).Returns(true);

        HttpClient httpClient = new(new BodyHandler(json));
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth, mockCache.Object);

        List<TidalTrackInfo> result = await client.GetAlbumTracksAsync("al1");

        Assert.Single(result);
        mockCache.Verify(c => c.Set(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<TidalAlbumTracksDto>(),
            TimeSpan.FromHours(2)), Times.Once);
    }

    /// <summary>
    /// Covers cache.Set for search with 5 minute duration.
    /// Proof: grep -n "_cache?.Set.*FromMinutes(5)" src/Tidalarr/Domain/Api/TidalApiClient.cs
    /// Line 190: this._cache?.Set(endpoint, parameters, dto, TimeSpan.FromMinutes(5));
    /// </summary>
    [Fact]
    public async Task SearchAsync_CacheSet_StoresWithFiveMinuteDuration()
    {
        string json = JsonSerializer.Serialize(new
        {
            albums = new { items = new object[] { } },
            tracks = new { items = new object[] { } },
            artists = new { items = new object[] { } }
        });

        Mock<IStreamingResponseCache> mockCache = new();
        mockCache.Setup(c => c.Get<TidalSearchResponseDto>(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns((TidalSearchResponseDto?)null);
        mockCache.Setup(c => c.ShouldCache(It.IsAny<string>())).Returns(true);

        HttpClient httpClient = new(new BodyHandler(json));
        ITidalAuth mockAuth = CreateMockAuth().Object;
        TidalApiClient client = new(httpClient, mockAuth, mockCache.Object);

        TidalSearchResults result = await client.SearchAsync("test");

        Assert.Empty(result.Albums);
        mockCache.Verify(c => c.Set(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<TidalSearchResponseDto>(),
            TimeSpan.FromMinutes(5)), Times.Once);
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

    private static byte[] CompressGzip(string json)
    {
        using MemoryStream ms = new();
        using (GZipStream gzip = new(ms, CompressionLevel.Optimal, leaveOpen: true))
        using (StreamWriter writer = new(gzip, Encoding.UTF8))
        {
            writer.Write(json);
        }
        return ms.ToArray();
    }

    private static byte[] CompressDeflate(string json)
    {
        using MemoryStream ms = new();
        using (DeflateStream deflate = new(ms, CompressionLevel.Optimal, leaveOpen: true))
        using (StreamWriter writer = new(deflate, Encoding.UTF8))
        {
            writer.Write(json);
        }
        return ms.ToArray();
    }

    private static byte[] CompressBrotli(string json)
    {
        using MemoryStream ms = new();
        using (BrotliStream brotli = new(ms, CompressionLevel.Optimal, leaveOpen: true))
        using (StreamWriter writer = new(brotli, Encoding.UTF8))
        {
            writer.Write(json);
        }
        return ms.ToArray();
    }

    #endregion

    #region Mock Handlers

    private class BodyHandler(string json) : HttpMessageHandler
    {
        private readonly string _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private class NullContentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = null!
            };
            return Task.FromResult(response);
        }
    }

    private class EncodingHeaderHandler(byte[] content, string encoding) : HttpMessageHandler
    {
        private readonly byte[] _content = content;
        private readonly string _encoding = encoding;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ByteArrayContent byteContent = new(_content);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            byteContent.Headers.ContentEncoding.Add(_encoding);

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = byteContent
            };
            return Task.FromResult(response);
        }
    }

    private class ByteArrayHandler(byte[] data) : HttpMessageHandler
    {
        private readonly byte[] _data = data;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ByteArrayContent byteContent = new(_data);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = byteContent
            };
            return Task.FromResult(response);
        }
    }

    #endregion
}
