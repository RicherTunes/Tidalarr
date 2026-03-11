using System.Net;
using System.Text.Json;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests;

public class TidalOAuthServiceTokenLifecycleTests
{
    [Fact]
    public async Task GetValidTokens_RefreshesWhenStoredExpired()
    {
        TidalTokens expired = new("old", "refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        MemoryTokenStorage storage = new(expired);
        Domain.Authentication.TidalTokenResponse refreshResponse = new("new_access", "new_refresh", "Bearer", 3600, new TidalUserResponse("sess2", "US", 123));
        HttpClient http = new(new FixedResponseHandler(JsonSerializer.Serialize(refreshResponse)));

        TidalOAuthService svc = new(http, storage);
        TidalTokens tokens = await svc.GetValidTokensAsync();
        Assert.Equal("new_access", tokens.AccessToken);
        Assert.Equal("new_refresh", tokens.RefreshToken);
    }

    [Fact]
    public async Task GetValidTokens_Throws_WhenNoStoredOrCurrentTokens()
    {
        MemoryTokenStorage storage = new(null);
        HttpClient http = new(new FixedResponseHandler("", HttpStatusCode.BadRequest));
        TidalOAuthService svc = new(http, storage);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(svc.GetValidTokensAsync);
    }

    [Fact]
    public async Task GetValidTokens_RepairsStoredTokensFromAccessTokenClaims_WhenSessionIdMissing()
    {
        string accessToken = CreateJwt(new Dictionary<string, object>
        {
            ["sid"] = "sess-from-jwt",
            ["cc"] = "CA"
        });

        TidalTokens stored = new(accessToken, "refresh", "Bearer", DateTime.UtcNow.AddMinutes(30), "", "", "u1");
        MemoryTokenStorage storage = new(stored);
        HttpClient http = new(new FixedResponseHandler("", HttpStatusCode.BadRequest));

        TidalOAuthService svc = new(http, storage);
        TidalTokens tokens = await svc.GetValidTokensAsync();

        Assert.Equal("sess-from-jwt", tokens.SessionId);
        Assert.Equal("CA", tokens.CountryCode);
        Assert.True(storage.SaveCount >= 1);
        Assert.Equal("sess-from-jwt", storage.LastSavedTokens?.SessionId);
    }

    private static string CreateJwt(Dictionary<string, object> payloadClaims)
    {
        string headerJson = JsonSerializer.Serialize(new Dictionary<string, object> { ["alg"] = "none", ["typ"] = "JWT" });
        string payloadJson = JsonSerializer.Serialize(payloadClaims);
        string header = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(headerJson));
        string payload = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(payloadJson));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

internal class MemoryTokenStorage(TidalTokens? initial) : ITokenStorage
{
    private TidalTokens? _tokens = initial;

    public int SaveCount { get; private set; }
    public TidalTokens? LastSavedTokens { get; private set; }

    public Task SaveTokensAsync(TidalTokens tokens)
    {
        SaveCount++;
        LastSavedTokens = tokens;
        this._tokens = tokens;
        return Task.CompletedTask;
    }
    public Task<TidalTokens?> LoadTokensAsync()
    {
        return Task.FromResult(this._tokens);
    }

    public Task DeleteTokensAsync() { this._tokens = null; return Task.CompletedTask; }
}

internal class FixedResponseHandler(string content, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
{
    private readonly string _content = content;
    private readonly HttpStatusCode _code = code;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // If this is a refresh token request, return our canned response
        return Task.FromResult(new HttpResponseMessage(this._code) { Content = new StringContent(this._content, System.Text.Encoding.UTF8, "application/json") });
    }
}


