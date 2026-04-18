using System.Net;
using System.Text;
using System.Text.Json;
using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class OAuthRefreshTokenRetentionTests
{
    [Fact]
    public async Task RefreshTokensAsync_ResponseMissingRefreshToken_RetainsExistingRefreshToken()
    {
        TidalTokenResponse mockResponse = new(
            access_token: "new_access_token",
            refresh_token: string.Empty,
            token_type: "Bearer",
            expires_in: 3600,
            user: new TidalUserResponse("session456", "US", 12345));

        HttpClient httpClient = CreateMockHttpClient(JsonSerializer.Serialize(mockResponse));
        PKCEGenerator pkceGenerator = new();
        TidalOAuthService oauthService = new(httpClient, pkceGenerator, new InMemoryTokenStorage());

        var tokens = await oauthService.RefreshTokensAsync("old_refresh_token");

        Assert.Equal("old_refresh_token", tokens.RefreshToken);
    }

    private static HttpClient CreateMockHttpClient(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpClient(new MockHttpMessageHandler(jsonResponse, statusCode));
    }

    private sealed class MockHttpMessageHandler(string response, HttpStatusCode statusCode) : HttpMessageHandler
    {
        private readonly string _response = response;
        private readonly HttpStatusCode _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(this._statusCode);
            if (!string.IsNullOrEmpty(this._response))
            {
                response.Content = new StringContent(this._response, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class InMemoryTokenStorage : ITokenStorage
    {
        private Core.Models.TidalTokens? _tokens;

        public Task SaveTokensAsync(Core.Models.TidalTokens tokens)
        {
            this._tokens = tokens;
            return Task.CompletedTask;
        }

        public Task<Core.Models.TidalTokens?> LoadTokensAsync()
        {
            return Task.FromResult(this._tokens);
        }

        public Task DeleteTokensAsync()
        {
            this._tokens = null;
            return Task.CompletedTask;
        }
    }
}
