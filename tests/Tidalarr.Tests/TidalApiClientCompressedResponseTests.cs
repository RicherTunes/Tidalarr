using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientCompressedResponseTests
{
    [Fact]
    public async Task TidalApiClientPlaybackInfoGzipBodyParses()
    {
        TidalPlaybackInfoDto playback = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("manifest")),
            manifestMimeType: "application/dash+xml",
            encryptionType: "NONE",
            securityToken: null);

        GzipPlaybackHandler handler = new(JsonSerializer.Serialize(playback));
        TidalApiClient client = new(new HttpClient(handler), new AuthStub());

        TidalStreamInfo result = await client.GetStreamInfoAsync("123", TidalQuality.High);

        Assert.Equal("123", result.TrackId);
        Assert.Equal("application/dash+xml", result.MimeType);
        Assert.Equal(".m4a", result.FileExtension);
    }

    private sealed class GzipPlaybackHandler(string json) : HttpMessageHandler
    {
        private readonly byte[] _body = Compress(json);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(this._body)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }

        private static byte[] Compress(string payload)
        {
            using MemoryStream buffer = new();
            using (GZipStream gzip = new(buffer, CompressionMode.Compress, leaveOpen: true))
            using (StreamWriter writer = new(gzip, Encoding.UTF8, leaveOpen: true))
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
        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
        {
            return Task.FromResult(new TidalAuthUrl("https://example/authorize", "verifier", "state", string.Empty));
        }

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
        {
            return Task.FromResult(this._tokens);
        }

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
        {
            return Task.FromResult(this._tokens);
        }

        public Task<TidalTokens> GetValidTokensAsync()
        {
            return Task.FromResult(this._tokens);
        }
    }
}



