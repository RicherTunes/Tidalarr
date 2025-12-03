using System.Net;
using System.Text.Json;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests;

public class TidalOAuthServiceTokenLifecycleTests
{
    [Fact]
    public async Task GetValidTokens_RefreshesWhenStoredExpired()
    {
        var expired = new TidalTokens("old", "refresh", "Bearer", DateTime.UtcNow.AddMinutes(-10), "sess", "US", "u1");
        var storage = new MemoryTokenStorage(expired);
        var refreshResponse = new Tidalarr.Core.Models.TidalTokenResponse("new_access", "new_refresh", "Bearer", 3600, new("sess2", "US", 123));
        var http = new HttpClient(new FixedResponseHandler(JsonSerializer.Serialize(refreshResponse)));

        var svc = new TidalOAuthService(http, storage);
        var tokens = await svc.GetValidTokensAsync();
        Assert.Equal("new_access", tokens.AccessToken);
        Assert.Equal("new_refresh", tokens.RefreshToken);
    }

    [Fact]
    public async Task GetValidTokens_Throws_WhenNoStoredOrCurrentTokens()
    {
        var storage = new MemoryTokenStorage(null);
        var http = new HttpClient(new FixedResponseHandler("", HttpStatusCode.BadRequest));
        var svc = new TidalOAuthService(http, storage);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetValidTokensAsync());
    }
}

class MemoryTokenStorage : ITokenStorage
{
    private TidalTokens? _tokens;
    public MemoryTokenStorage(TidalTokens? initial) { _tokens = initial; }
    public Task SaveTokensAsync(TidalTokens tokens) { _tokens = tokens; return Task.CompletedTask; }
    public Task<TidalTokens?> LoadTokensAsync() => Task.FromResult(_tokens);
    public Task DeleteTokensAsync() { _tokens = null; return Task.CompletedTask; }
}

class FixedResponseHandler : HttpMessageHandler
{
    private readonly string _content;
    private readonly HttpStatusCode _code;
    public FixedResponseHandler(string content, HttpStatusCode code = HttpStatusCode.OK) { _content = content; _code = code; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // If this is a refresh token request, return our canned response
        return Task.FromResult(new HttpResponseMessage(_code) { Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json") });
    }
}




