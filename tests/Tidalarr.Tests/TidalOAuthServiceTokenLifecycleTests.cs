using System.Net;
using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Authentication;

namespace Tidalarr.Tests;

[Trait("Category", "Integration")]
[Trait("Area", "E2E/Hermetic")]
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
    public async Task GetValidTokens_RefreshesWhenStoredTokenInsideProactiveBuffer()
    {
        TidalTokens nearExpiry = new("old_access", "old_refresh", "Bearer", DateTime.UtcNow.AddMinutes(1), "sess", "US", "u1");
        MemoryTokenStorage storage = new(nearExpiry);
        Domain.Authentication.TidalTokenResponse refreshResponse = new("new_access", "new_refresh", "Bearer", 3600, new TidalUserResponse("sess2", "US", 123));
        DelayedCountingResponseHandler handler = new(JsonSerializer.Serialize(refreshResponse), TimeSpan.Zero);
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        TidalTokens tokens = await svc.GetValidTokensAsync();

        Assert.Equal("new_access", tokens.AccessToken);
        Assert.Equal("new_refresh", tokens.RefreshToken);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("new_refresh", storage.LastSavedTokens?.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenCalledConcurrently_SharesSingleRefreshRequest()
    {
        TidalTokens stored = new("old_access", "old_refresh", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "u1");
        MemoryTokenStorage storage = new(stored);
        Domain.Authentication.TidalTokenResponse refreshResponse = new("new_access", "new_refresh", "Bearer", 3600, new TidalUserResponse("sess2", "US", 123));
        DelayedCountingResponseHandler handler = new(JsonSerializer.Serialize(refreshResponse), TimeSpan.FromMilliseconds(50));
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        string[] accessTokens = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ((IStreamingTokenProvider)svc).RefreshTokenAsync()));

        Assert.All(accessTokens, accessToken => Assert.Equal("new_access", accessToken));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshTokensAsync_WhenCalledConcurrentlyWithSameToken_SharesSingleRefreshRequest()
    {
        Domain.Authentication.TidalTokenResponse refreshResponse = new("new_access", "new_refresh", "Bearer", 3600, new TidalUserResponse("sess2", "US", 123));
        DelayedCountingResponseHandler handler = new(JsonSerializer.Serialize(refreshResponse), TimeSpan.FromMilliseconds(50));
        TidalOAuthService svc = new(new HttpClient(handler), new MemoryTokenStorage(null));

        TidalTokens[] tokens = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => svc.RefreshTokensAsync("old_refresh")));

        Assert.All(tokens, token => Assert.Equal("new_access", token.AccessToken));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshTokensAsync_WhenCalledConcurrentlyWithDifferentTokens_DoesNotShareRefreshResult()
    {
        Dictionary<string, Domain.Authentication.TidalTokenResponse> responses = new()
        {
            ["old_refresh_a"] = new("access_a", "new_refresh_a", "Bearer", 3600, new TidalUserResponse("sess-a", "US", 101)),
            ["old_refresh_b"] = new("access_b", "new_refresh_b", "Bearer", 3600, new TidalUserResponse("sess-b", "CA", 202))
        };
        RoutingRefreshResponseHandler handler = new(responses, TimeSpan.FromMilliseconds(50));
        TidalOAuthService svc = new(new HttpClient(handler), new MemoryTokenStorage(null));

        TidalTokens[] tokens = await Task.WhenAll(
            svc.RefreshTokensAsync("old_refresh_a"),
            svc.RefreshTokensAsync("old_refresh_b"));

        Assert.Contains(tokens, token => token.AccessToken == "access_a");
        Assert.Contains(tokens, token => token.AccessToken == "access_b");
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task RefreshTokenAsync_AndDirectRefreshWithSameToken_ShareSingleRefreshRequest()
    {
        TidalTokens stored = new("old_access", "old_refresh", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "u1");
        MemoryTokenStorage storage = new(stored);
        Domain.Authentication.TidalTokenResponse refreshResponse = new("new_access", "new_refresh", "Bearer", 3600, new TidalUserResponse("sess2", "US", 123));
        SingleUseRefreshResponseHandler handler = new(JsonSerializer.Serialize(refreshResponse), TimeSpan.FromMilliseconds(50));
        TidalOAuthService svc = new(new HttpClient(handler), storage);

        Task<string> streamingRefresh = ((IStreamingTokenProvider)svc).RefreshTokenAsync();
        Task<TidalTokens> directRefresh = svc.RefreshTokensAsync("old_refresh");

        string streamingAccessToken = await streamingRefresh;
        TidalTokens directTokens = await directRefresh;

        Assert.Equal("new_access", streamingAccessToken);
        Assert.Equal("new_access", directTokens.AccessToken);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task OAuthDelegatingHandler_WhenLate401ArrivesAfterRotation_ReusesCurrentToken()
    {
        TidalTokens stored = new("old_access", "old_refresh", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "u1");
        MemoryTokenStorage storage = new(stored);
        Domain.Authentication.TidalTokenResponse refreshResponse = new(
            "new_access",
            "new_refresh",
            "Bearer",
            3600,
            new TidalUserResponse("sess2", "US", 123));
        GatedSingleUseRefreshResponseHandler refreshHandler = new(JsonSerializer.Serialize(refreshResponse));
        TidalOAuthService tokenProvider = new(new HttpClient(refreshHandler), storage);
        Late401ResourceHandler resourceHandler = new();
        OAuthDelegatingHandler oauthHandler = new(tokenProvider, NullLogger.Instance)
        {
            InnerHandler = resourceHandler
        };
        using HttpClient client = new(oauthHandler, disposeHandler: true);

        Task<HttpResponseMessage> first = client.GetAsync("https://api.test/resource1");
        Task<HttpResponseMessage> second = client.GetAsync("https://api.test/resource2");
        Task<HttpResponseMessage[]> both = Task.WhenAll(first, second);

        try
        {
            await resourceHandler.OriginalRequestsObserved.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(["old_access", "old_access"], resourceHandler.OriginalTokens.OrderBy(token => token).ToArray());

            resourceHandler.ReleaseFirstUnauthorizedResponse();
            await refreshHandler.RefreshRequestStarted.WaitAsync(TimeSpan.FromSeconds(5));
            refreshHandler.ReleaseRefreshResponse();
            await resourceHandler.FirstRetryObserved.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single(resourceHandler.RetryTokens);
            resourceHandler.ReleaseSecondUnauthorizedResponse();

            HttpResponseMessage[] responses = await both;
            try
            {
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            }
            finally
            {
                foreach (HttpResponseMessage response in responses)
                {
                    response.Dispose();
                }
            }

            Assert.Equal(1, refreshHandler.RequestCount);
            Assert.Equal(["new_access", "new_access"], resourceHandler.RetryTokens.OrderBy(token => token).ToArray());
            Assert.Equal("new_refresh", storage.LastSavedTokens?.RefreshToken);
        }
        finally
        {
            resourceHandler.ReleaseFirstUnauthorizedResponse();
            resourceHandler.ReleaseSecondUnauthorizedResponse();
            refreshHandler.ReleaseRefreshResponse();
            try
            {
                await both.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Preserve the primary assertion or timeout failure while ensuring blocked fixture tasks join.
            }
        }
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

    [Fact]
    public async Task ClearAuthenticationCache_DoesNotDeleteStoredRefreshToken()
    {
        TidalTokens stored = new("old_access", "old_refresh", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "u1");
        MemoryTokenStorage storage = new(stored);
        TidalOAuthService svc = new(new HttpClient(new FixedResponseHandler("", HttpStatusCode.BadRequest)), storage);

        await svc.GetValidTokensAsync();
        ((IStreamingTokenProvider)svc).ClearAuthenticationCache();

        TokenEnvelope<TidalTokens>? persisted = await storage.LoadAsync();
        Assert.NotNull(persisted);
        Assert.Equal("old_refresh", persisted!.Session.RefreshToken);
        Assert.Equal(0, storage.ClearCount);
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

internal class MemoryTokenStorage : ITokenStore<TidalTokens>
{
    private TokenEnvelope<TidalTokens>? _envelope;

    public MemoryTokenStorage(TidalTokens? initial)
    {
        if (initial is not null)
        {
            this._envelope = new TokenEnvelope<TidalTokens>(initial, initial.ExpiresAt);
        }
    }

    public int SaveCount { get; private set; }
    public TidalTokens? LastSavedTokens { get; private set; }
    public int ClearCount { get; private set; }

    public Task SaveAsync(TokenEnvelope<TidalTokens> envelope, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        LastSavedTokens = envelope.Session;
        this._envelope = envelope;
        return Task.CompletedTask;
    }

    public Task<TokenEnvelope<TidalTokens>?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this._envelope);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ClearCount++;
        this._envelope = null;
        return Task.CompletedTask;
    }
}

internal sealed class DelayedCountingResponseHandler(string content, TimeSpan delay, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
{
    private readonly string content = content;
    private readonly TimeSpan delay = delay;
    private readonly HttpStatusCode code = code;
    private int requestCount;

    public int RequestCount => Volatile.Read(ref this.requestCount);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref this.requestCount);
        if (this.delay > TimeSpan.Zero)
        {
            await Task.Delay(this.delay, cancellationToken);
        }

        return new HttpResponseMessage(this.code)
        {
            Content = new StringContent(this.content, System.Text.Encoding.UTF8, "application/json")
        };
    }
}

internal sealed class RoutingRefreshResponseHandler(
    IReadOnlyDictionary<string, Domain.Authentication.TidalTokenResponse> responses,
    TimeSpan delay) : HttpMessageHandler
{
    private readonly IReadOnlyDictionary<string, Domain.Authentication.TidalTokenResponse> responses = responses;
    private readonly TimeSpan delay = delay;
    private int requestCount;

    public int RequestCount => Volatile.Read(ref this.requestCount);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref this.requestCount);
        if (this.delay > TimeSpan.Zero)
        {
            await Task.Delay(this.delay, cancellationToken);
        }

        string body = request.Content == null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        string? refreshToken = this.responses.Keys.FirstOrDefault(token =>
            body.Contains($"refresh_token={Uri.EscapeDataString(token)}", StringComparison.Ordinal));

        return refreshToken == null
            ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"unknown_refresh_token\"}", System.Text.Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(this.responses[refreshToken]), System.Text.Encoding.UTF8, "application/json")
            };
    }
}

internal sealed class SingleUseRefreshResponseHandler(string successContent, TimeSpan delay) : HttpMessageHandler
{
    private readonly string successContent = successContent;
    private readonly TimeSpan delay = delay;
    private int requestCount;

    public int RequestCount => Volatile.Read(ref this.requestCount);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int count = Interlocked.Increment(ref this.requestCount);
        if (this.delay > TimeSpan.Zero)
        {
            await Task.Delay(this.delay, cancellationToken);
        }

        return count == 1
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(this.successContent, System.Text.Encoding.UTF8, "application/json")
            }
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}", System.Text.Encoding.UTF8, "application/json")
            };
    }
}

internal sealed class GatedSingleUseRefreshResponseHandler(string successContent) : HttpMessageHandler
{
    private readonly string successContent = successContent;
    private readonly TaskCompletionSource<bool> refreshRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> releaseRefreshResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int requestCount;

    public int RequestCount => Volatile.Read(ref this.requestCount);
    public Task RefreshRequestStarted => this.refreshRequestStarted.Task;

    public void ReleaseRefreshResponse()
    {
        this.releaseRefreshResponse.TrySetResult(true);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int count = Interlocked.Increment(ref this.requestCount);
        this.refreshRequestStarted.TrySetResult(true);
        await this.releaseRefreshResponse.Task.WaitAsync(cancellationToken);

        return count == 1
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(this.successContent, System.Text.Encoding.UTF8, "application/json")
            }
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}", System.Text.Encoding.UTF8, "application/json")
            };
    }
}

internal sealed class Late401ResourceHandler : HttpMessageHandler
{
    private readonly object sync = new();
    private readonly Dictionary<string, int> callsByPath = new(StringComparer.Ordinal);
    private readonly List<string> originalTokens = [];
    private readonly List<string> retryTokens = [];
    private readonly TaskCompletionSource<bool> originalRequestsObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> firstRetryObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> releaseFirstUnauthorized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> releaseSecondUnauthorized = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task OriginalRequestsObserved => this.originalRequestsObserved.Task;
    public Task FirstRetryObserved => this.firstRetryObserved.Task;

    public IReadOnlyList<string> OriginalTokens
    {
        get
        {
            lock (this.sync)
            {
                return this.originalTokens.ToArray();
            }
        }
    }

    public IReadOnlyList<string> RetryTokens
    {
        get
        {
            lock (this.sync)
            {
                return this.retryTokens.ToArray();
            }
        }
    }

    public void ReleaseFirstUnauthorizedResponse()
    {
        this.releaseFirstUnauthorized.TrySetResult(true);
    }

    public void ReleaseSecondUnauthorizedResponse()
    {
        this.releaseSecondUnauthorized.TrySetResult(true);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath ?? string.Empty;
        string token = request.Headers.Authorization?.Parameter ?? string.Empty;
        int call;
        int originalSequence = 0;
        lock (this.sync)
        {
            this.callsByPath.TryGetValue(path, out int previous);
            call = previous + 1;
            this.callsByPath[path] = call;
            if (call == 1)
            {
                this.originalTokens.Add(token);
                originalSequence = this.originalTokens.Count;
                if (this.originalTokens.Count == 2)
                {
                    this.originalRequestsObserved.TrySetResult(true);
                }
            }
            else
            {
                this.retryTokens.Add(token);
            }
        }

        if (call == 1)
        {
            Task release = originalSequence == 1
                ? this.releaseFirstUnauthorized.Task
                : this.releaseSecondUnauthorized.Task;
            await release.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
        }

        this.firstRetryObserved.TrySetResult(true);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent("ok")
        };
    }
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
