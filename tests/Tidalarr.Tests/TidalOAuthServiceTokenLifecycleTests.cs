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
        TidalTokens expired = new TidalTokens("old", "refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        MemoryTokenStorage storage = new MemoryTokenStorage(expired);
        var refreshResponse = new TidalTokenResponse("new_access", "new_refresh", "Bearer", 3600, new("sess2", "US", 123));
        HttpClient http = new HttpClient(new FixedResponseHandler(JsonSerializer.Serialize(refreshResponse)));

        TidalOAuthService svc = new TidalOAuthService(http, storage);
        TidalTokens tokens = await svc.GetValidTokensAsync();
        Assert.Equal("new_access", tokens.AccessToken);
        Assert.Equal("new_refresh", tokens.RefreshToken);
    }

    [Fact]
    public async Task GetValidTokens_Throws_WhenNoStoredOrCurrentTokens()
    {
        MemoryTokenStorage storage = new MemoryTokenStorage(null);
        HttpClient http = new HttpClient(new FixedResponseHandler("", HttpStatusCode.BadRequest));
        TidalOAuthService svc = new TidalOAuthService(http, storage);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(svc.GetValidTokensAsync);
    }
}

internal class MemoryTokenStorage(TidalTokens? initial) : ITokenStorage
{
    private TidalTokens? _tokens = initial;

    public Task SaveTokensAsync(TidalTokens tokens) { this._tokens = tokens; return Task.CompletedTask; }
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




