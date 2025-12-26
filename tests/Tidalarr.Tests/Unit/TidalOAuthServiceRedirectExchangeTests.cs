using System.Net;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

public class TidalOAuthServiceRedirectExchangeTests
{
    [Fact]
    public async Task GetValidTokensAsync_WithRedirectUrlAndPkceState_ExchangesAndPersistsTokens()
    {
        string dir = Path.Combine(Path.GetTempPath(), "tidalarr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            // Arrange: persisted PKCE state created when AuthUrl was generated in UI.
            string codeVerifier = new string('a', 128);
            string state = "test_state";

            var pkceStore = new PkceStateFileStore(dir);
            pkceStore.Save(new PkceState(
                CodeVerifier: codeVerifier,
                State: state,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                AuthorizationUrl: TidalAuthUrlHelper.GetAuthorizationUrl(dir)));

            var tokenResponse = new Tidalarr.Domain.Authentication.TidalTokenResponse(
                access_token: "test_access_token",
                refresh_token: "test_refresh_token",
                token_type: "Bearer",
                expires_in: 3600,
                user: new Tidalarr.Domain.Authentication.TidalUserResponse("session123", "US", 12345));

            HttpClient httpClient = new(new JsonHandler(JsonSerializer.Serialize(tokenResponse)));
            var storage = new InMemoryTokenStorage();

            var settings = new TidalarrSettings
            {
                ConfigPath = dir,
                RedirectUrl = $"https://tidal.com/android/login/auth?code=test_auth_code&state={state}"
            };

            TidalOAuthService svc = new(httpClient, storage, settings);

            // Act
            TidalTokens tokens = await svc.GetValidTokensAsync();

            // Assert
            Assert.Equal("test_access_token", tokens.AccessToken);
            Assert.Equal("test_refresh_token", tokens.RefreshToken);
            Assert.NotNull(storage.Tokens);
            Assert.Equal("test_access_token", storage.Tokens!.AccessToken);

            Assert.False(File.Exists(pkceStore.StatePath));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private sealed class JsonHandler(string json, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(code)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class InMemoryTokenStorage : ITokenStorage
    {
        public TidalTokens? Tokens { get; private set; }

        public Task SaveTokensAsync(TidalTokens tokens)
        {
            Tokens = tokens;
            return Task.CompletedTask;
        }

        public Task<TidalTokens?> LoadTokensAsync()
        {
            return Task.FromResult(Tokens);
        }

        public Task DeleteTokensAsync()
        {
            Tokens = null;
            return Task.CompletedTask;
        }
    }
}

