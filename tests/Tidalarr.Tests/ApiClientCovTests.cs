using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Lidarr.Plugin.Abstractions.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalApiClient - targets uncovered branches and edge cases.
/// Source: src/Tidalarr/Domain/Api/TidalApiClient.cs
/// </summary>
public class ApiClientCoverageTests
{
    #region Constructor ArgumentNullException Tests (lines 20, 23)

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // Arrange
        Mock<ITidalAuth> mockAuth = new();

        // Act & Assert - Line 20: throw new ArgumentNullException(nameof(httpClient))
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new TidalApiClient(null!, mockAuth.Object));

        // Proof: grep -n "throw new ArgumentNullException" src/Tidalarr/Domain/Api/TidalApiClient.cs
        // Line 20: private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Assert.Equal("httpClient", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullAuthService_ThrowsArgumentNullException()
    {
        // Arrange
        HttpClient httpClient = new();

        // Act & Assert - Line 23: throw new ArgumentNullException(nameof(authService))
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new TidalApiClient(httpClient, null!));

        // Proof: grep -n "throw new ArgumentNullException" src/Tidalarr/Domain/Api/TidalApiClient.cs
        // Line 23: private readonly ITidalAuth _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        Assert.Equal("authService", ex.ParamName);
    }

    #endregion

    #region DI Constructor Tests (lines 29-36)

    [Fact]
    public async Task Constructor_WithManifestParser_ParsesManifestInGetStreamInfoAsync()
    {
        // Arrange - Test lines 220-238: manifest parser branch in GetStreamInfoAsync
        string dashManifest = """<?xml version="1.0"?><MPD><Period><AdaptationSet><SegmentTemplate media="https://example.com/chunk$Number$.m4a"/></AdaptationSet></Period></MPD>""";
        TidalPlaybackInfoDto playbackDto = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes(dashManifest)),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playbackDto), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();
        TidalManifestParser manifestParser = new();
        Mock<ILogger<TidalApiClient>> mockLogger = new();

        // Act - Use DI constructor (lines 29-36)
        TidalApiClient client = new(httpClient, mockAuth.Object, manifestParser, logger: mockLogger.Object);

        TidalStreamInfo result = await client.GetStreamInfoAsync("track123", TidalQuality.Lossless);

        // Assert - Line 226-232: manifest parser path returns parsed chunk URLs
        Assert.Equal("track123", result.TrackId);
        Assert.Equal(".m4a", result.FileExtension);
        Assert.Equal("application/dash+xml", result.MimeType);
        Assert.False(result.IsEncrypted);
    }

    [Fact]
    public void Constructor_WithRateLimitReporter_AcceptsRateLimitReporter()
    {
        // Arrange - Test lines 29-36: DI constructor accepts rateLimitReporter
        TidalPlaybackInfoDto playbackDto = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("<MPD/>")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playbackDto), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();
        Mock<IRateLimitReporter> mockReporter = new();
        mockReporter.SetupGet(r => r.Status).Returns(new RateLimitStatus { IsRateLimited = false });

        // Act - Use DI constructor with rateLimitReporter (line 30)
        TidalApiClient client = new(httpClient, mockAuth.Object, manifestParser: null, rateLimitReporter: mockReporter.Object);

        // Assert - Constructor should accept the rateLimitReporter parameter
        Assert.NotNull(client);
    }

    [Fact]
    public async Task RateLimitReporter_ClearsRateLimit_OnSuccessAfter429()
    {
        // Arrange - Test lines 529-532: ReportRateLimitClearedAsync when IsRateLimited and success
        TidalPlaybackInfoDto playbackDto = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("<MPD/>")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playbackDto), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();
        Mock<IRateLimitReporter> mockReporter = new();
        mockReporter.SetupGet(r => r.Status).Returns(new RateLimitStatus { IsRateLimited = true });

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object, manifestParser: null, rateLimitReporter: mockReporter.Object);
        await client.GetStreamInfoAsync("t1", TidalQuality.High);

        // Assert - Line 531: await this._rateLimitReporter.ReportRateLimitClearedAsync()
        mockReporter.Verify(r => r.ReportRateLimitClearedAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SearchAsync with countryCode (lines 158-192)

    [Fact]
    public async Task SearchAsync_WithCountryCode_UsesProvidedCountryCode()
    {
        // Arrange - Test line 168: countryCode ?? tokens.CountryCode
        TidalSearchResponseDto searchDto = new(new([]), new([]));
        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(searchDto), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalSearchResults result = await client.SearchAsync("query", limit: 10, countryCode: "DE");

        // Assert - Line 168 uses "DE" instead of default "US"
        Assert.Contains("countryCode=DE", handler.LastRequest?.RequestUri?.Query);
        Assert.NotNull(result);
    }

    #endregion

    #region IsAuthenticatedAsync Tests (lines 272-283)

    [Fact]
    public async Task IsAuthenticatedAsync_ReturnsTrue_WhenAuthenticated()
    {
        // Arrange - Test lines 276-277: successful auth path
        Mock<ITidalAuth> mockAuth = new();
        mockAuth.Setup(a => a.GetValidTokensAsync())
            .ReturnsAsync(new TidalTokens("access_token", "refresh", "Bearer", DateTime.UtcNow.AddHours(1), "session", "US", "user1"));

        TidalApiClient client = new(new HttpClient(), mockAuth.Object);

        // Act
        bool result = await client.IsAuthenticatedAsync();

        // Assert - Line 277: return !string.IsNullOrEmpty(tokens?.AccessToken)
        Assert.True(result);
    }

    [Fact]
    public async Task IsAuthenticatedAsync_ReturnsFalse_OnException()
    {
        // Arrange - Test lines 279-282: catch block returns false
        Mock<ITidalAuth> mockAuth = new();
        mockAuth.Setup(a => a.GetValidTokensAsync()).ThrowsAsync(new InvalidOperationException("Auth failed"));

        TidalApiClient client = new(new HttpClient(), mockAuth.Object);

        // Act
        bool result = await client.IsAuthenticatedAsync();

        // Assert - Line 281: return false
        Assert.False(result);
    }

    #endregion

    #region GetAlbumWithTracksAsync Tests (lines 139-152)

    [Fact]
    public async Task GetAlbumWithTracksAsync_ReturnsAlbumWithTracks()
    {
        // Arrange - Test lines 141-152: combines GetAlbumAsync + GetAlbumTracksAsync
        // Note: IDs must be numeric since TidalAlbumDto.id uses FlexibleLongJsonConverter
        TidalAlbumDto albumDto = new("12345", "Test Album", new("Artist", "100"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 10, 3000, true, "cover");
        TidalTrackDto trackDto = new("789", "Track 1", new("Artist", "100"), albumDto, 1, 180, true, "LOSSLESS");
        TidalAlbumTracksDto tracksDto = new([trackDto], 1);

        int callCount = 0;
        MockHttpMessageHandler handler = new(req =>
        {
            callCount++;
            if (req.RequestUri?.AbsolutePath.Contains("/tracks") == true)
            {
                return JsonSerializer.Serialize(tracksDto);
            }
            return JsonSerializer.Serialize(albumDto);
        }, HttpStatusCode.OK);

        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();
        TidalApiClient client = new(httpClient, mockAuth.Object);

        // Act
        TidalAlbumInfo result = await client.GetAlbumWithTracksAsync("12345");

        // Assert - Lines 143-151: returns combined album with tracks
        Assert.Equal("12345", result.Id);
        Assert.Equal("Test Album", result.Title);
        Assert.Single(result.Tracks);
        Assert.Equal("789", result.Tracks[0].Id);
    }

    #endregion

    #region GetPlaybackInfoAsync Tests (lines 248-271)

    [Fact]
    public async Task GetPlaybackInfoAsync_ReturnsParsedDto()
    {
        // Arrange - Test lines 248-271: raw playback info fetch
        TidalPlaybackInfoDto playbackDto = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("test-manifest")),
            manifestMimeType: "application/vnd.tidal.bts",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playbackDto), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalPlaybackInfoDto result = await client.GetPlaybackInfoAsync("track1", TidalQuality.Lossless);

        // Assert - Line 270: return dto ?? throw...
        Assert.Equal("application/vnd.tidal.bts", result.manifestMimeType);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_NullJson_ThrowsInvalidOperationException()
    {
        // Arrange - Test line 270: throw new InvalidOperationException("Failed to parse playback info")
        // When JSON is valid "null", deserialization succeeds but dto is null
        MockHttpMessageHandler handler = new("null", HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act & Assert
        TidalApiClient client = new(httpClient, mockAuth.Object);
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetPlaybackInfoAsync("track1", TidalQuality.Lossless));

        // Proof: grep -n "Failed to parse playback info" src/Tidalarr/Domain/Api/TidalApiClient.cs
        // Line 270: return dto ?? throw new InvalidOperationException("Failed to parse playback info");
        Assert.Contains("Failed to parse playback info", ex.Message);
    }

    #endregion

    #region Compression Tests - Deflate and Brotli (lines 468-489)

    [Fact]
    public async Task ReadContentAsStringAsync_DecompressesDeflate()
    {
        // Arrange - Test lines 472-475: deflate decompression path
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        DeflateHandler handler = new(JsonSerializer.Serialize(playback));
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.High);

        // Assert - Lines 472-475: deflate decompression successful
        Assert.Equal("123", result.TrackId);
        Assert.Equal("application/dash+xml", result.MimeType);
    }

    [Fact]
    public async Task ReadContentAsStringAsync_DecompressesBrotli()
    {
        // Arrange - Test lines 476-479: brotli decompression path
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        BrotliHandler handler = new(JsonSerializer.Serialize(playback));
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.High);

        // Assert - Lines 476-479: brotli decompression successful
        Assert.Equal("123", result.TrackId);
        Assert.Equal("application/dash+xml", result.MimeType);
    }

    [Fact]
    public async Task ReadContentAsStringAsync_FallsBackToUtf8_OnDecompressionError()
    {
        // Arrange - Test lines 481-489: fallback to UTF-8 on InvalidDataException/IOException
        // Send invalid gzip bytes that look like gzip but aren't valid
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        string json = JsonSerializer.Serialize(playback);
        // Create content that claims gzip but isn't actually compressed
        CorruptedCompressionHandler handler = new(json, "gzip");
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.High);

        // Assert - Falls back to UTF-8 and parses successfully
        Assert.Equal("123", result.TrackId);
    }

    #endregion

    #region InferPlaybackExtension Tests (lines 439-447)

    [Fact]
    public async Task GetStreamInfoAsync_ReturnsFlacExtension_ForFlacMimeType()
    {
        // Arrange - Test lines 444-445: flac extension inference
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "audio/flac",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playback), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.Lossless);

        // Assert - Line 445: return ".flac" for flac mimetype
        Assert.Equal(".flac", result.FileExtension);
    }

    [Fact]
    public async Task GetStreamInfoAsync_ReturnsM4aExtension_ForMpegMimeType()
    {
        // Arrange - Test line 442: m4a extension for mpeg
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "audio/mpeg",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playback), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.Lossless);

        // Assert - Line 443: return ".m4a" for mpeg mimetype
        Assert.Equal(".m4a", result.FileExtension);
    }

    [Fact]
    public async Task GetStreamInfoAsync_ReturnsM4aExtension_ForUnknownMimeType()
    {
        // Arrange - Test line 446: default to m4a for unknown
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "audio/unknown",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playback), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.Lossless);

        // Assert - Line 446: default return ".m4a"
        Assert.Equal(".m4a", result.FileExtension);
    }

    #endregion

    #region Encryption Detection Tests (lines 216-218)

    [Fact]
    public async Task GetStreamInfoAsync_DetectsEncryption_WhenEncryptionTypeIsNotNone()
    {
        // Arrange - Test lines 216-218: isEncrypted detection
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "AES",
            securityToken: "token123");

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playback), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.Lossless);

        // Assert - Line 217: isEncrypted = !string.IsNullOrWhiteSpace(encryptionType) && !string.Equals(encryptionType, "NONE"...)
        Assert.True(result.IsEncrypted);
        Assert.Equal("token123", result.SecurityToken);
    }

    [Fact]
    public async Task GetStreamInfoAsync_DetectsNoEncryption_WhenEncryptionTypeIsNone()
    {
        // Arrange
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playback), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.Lossless);

        // Assert
        Assert.False(result.IsEncrypted);
    }

    #endregion

    #region Manifest Parser Fallback Tests (lines 234-238)

    [Fact]
    public async Task GetStreamInfoAsync_FallsBack_WhenManifestParserThrows()
    {
        // Arrange - Test lines 234-238: catch block fallback to legacy behavior
        // Use invalid manifest that will fail parsing
        TidalPlaybackInfoDto playback = new(
            manifest: "not-valid-base64!@#$",
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(playback), HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();
        TidalManifestParser parser = new(); // Will throw on invalid base64

        // Act
        TidalApiClient client = new(httpClient, mockAuth.Object, parser);
        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.Lossless);

        // Assert - Falls back to legacy behavior (lines 239-246)
        Assert.Equal("123", result.TrackId);
        Assert.Empty(result.ChunkUrls); // Legacy path returns empty chunk URLs
    }

    #endregion

    #region Dispose Tests (lines 535-538)

    [Fact]
    public void Dispose_DisposesHttpClient()
    {
        // Arrange - Test lines 535-538: Dispose pattern
        MockHttpMessageHandler handler = new("{}", HttpStatusCode.OK);
        HttpClient httpClient = new(handler);
        Mock<ITidalAuth> mockAuth = CreateMockAuth();
        TidalApiClient client = new(httpClient, mockAuth.Object);

        // Act - Line 537: this._httpClient?.Dispose()
        client.Dispose();

        // Assert - Second dispose should not throw (handles re-entrancy)
        client.Dispose();
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

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _responseFactory;
        private readonly HttpStatusCode _statusCode;
        private TimeSpan? _retryAfter;

        public HttpRequestMessage? LastRequest { get; private set; }

        public MockHttpMessageHandler(string response, HttpStatusCode statusCode)
            : this(_ => response, statusCode)
        {
        }

        public MockHttpMessageHandler(Func<HttpRequestMessage, string> responseFactory, HttpStatusCode statusCode)
        {
            _responseFactory = responseFactory;
            _statusCode = statusCode;
        }

        public void SetRetryAfter(TimeSpan retryAfter) => _retryAfter = retryAfter;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            HttpResponseMessage response = new(_statusCode)
            {
                Content = new StringContent(_responseFactory(request), Encoding.UTF8, "application/json")
            };

            if (_retryAfter.HasValue)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(_retryAfter.Value);
            }

            return Task.FromResult(response);
        }
    }

    private class DeflateHandler(string json) : HttpMessageHandler
    {
        private readonly byte[] _compressedBody = CompressDeflate(json);

        private static byte[] CompressDeflate(string payload)
        {
            using MemoryStream buffer = new();
            using (DeflateStream deflate = new(buffer, CompressionMode.Compress, leaveOpen: true))
            using (StreamWriter writer = new(deflate, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(payload);
            }
            return buffer.ToArray();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_compressedBody)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response.Content.Headers.ContentEncoding.Add("deflate");
            return Task.FromResult(response);
        }
    }

    private class BrotliHandler(string json) : HttpMessageHandler
    {
        private readonly byte[] _compressedBody = CompressBrotli(json);

        private static byte[] CompressBrotli(string payload)
        {
            using MemoryStream buffer = new();
            using (BrotliStream brotli = new(buffer, CompressionMode.Compress, leaveOpen: true))
            using (StreamWriter writer = new(brotli, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(payload);
            }
            return buffer.ToArray();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_compressedBody)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response.Content.Headers.ContentEncoding.Add("br");
            return Task.FromResult(response);
        }
    }

    private class CorruptedCompressionHandler(string json, string encoding) : HttpMessageHandler
    {
        private readonly byte[] _body = Encoding.UTF8.GetBytes(json); // Not actually compressed
        private readonly string _encoding = encoding;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_body)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response.Content.Headers.ContentEncoding.Add(_encoding);
            return Task.FromResult(response);
        }
    }

    #endregion
}
