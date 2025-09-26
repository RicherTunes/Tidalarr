using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;
using Xunit;

namespace Tidalarr.Tests;

public class TidalApiClientCompressedResponseTests
{
    [Fact]
    public async Task TidalApiClientPlaybackInfoGzipBodyParses()
    {
        var playback = new TidalPlaybackInfoDto(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        var handler = new GzipPlaybackHandler(JsonSerializer.Serialize(playback));
        var client = new TidalApiClient(new HttpClient(handler), new AuthStub());

        var result = await client.GetStreamInfoAsync("123", TidalQuality.High);

        Assert.Equal("123", result.TrackId);
        Assert.Equal("application/dash+xml", result.MimeType);
        Assert.Equal(".m4a", result.FileExtension);
    }

    private sealed class GzipPlaybackHandler : HttpMessageHandler
    {
        private readonly byte[] _body;

        public GzipPlaybackHandler(string json)
        {
            _body = Compress(json);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_body)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }

        private static byte[] Compress(string payload)
        {
            using var buffer = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
            using (var writer = new StreamWriter(gzip, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(payload);
            }
            return buffer.ToArray();
        }
    }

    private sealed class AuthStub : ITidalAuth
    {
        private readonly TidalTokens _tokens = new(
            AccessToken: "access",
            RefreshToken: "refresh",
            TokenType: "Bearer",
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            SessionId: "session",
            CountryCode: "US",
            UserId: "user");

        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync() => Task.FromResult(new TidalAuthUrl("https://example/authorize", "verifier", "state", string.Empty));
        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier) => Task.FromResult(_tokens);
        public Task<TidalTokens> RefreshTokensAsync(string refreshToken) => Task.FromResult(_tokens);
        public Task<TidalTokens> GetValidTokensAsync() => Task.FromResult(_tokens);
    }
}


