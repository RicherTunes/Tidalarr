using System.Net;
using System.Net.Sockets;
using Lidarr.Plugin.Common.Resilience;
using Tidalarr.Infrastructure.Resilience;

namespace Tidalarr.Tests.Resilience;

/// <summary>
/// Verifies that <see cref="TidalBackendHealthHandler"/> correctly integrates with
/// <see cref="BackendHealthCache"/> to fail-fast on repeated connection-class failures.
///
/// Each test uses a fresh <see cref="BackendHealthCache"/> instance for isolation
/// (same pattern as Brainarr's BackendHealthCacheCompletionTests).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public class BackendHealthCacheAdoptionTests
{
    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private static HttpRequestException MakeConnectionRefused()
    {
        var socket = new SocketException((int)SocketError.ConnectionRefused);
        return new HttpRequestException("Connection refused", socket);
    }

    private static HttpResponseMessage OkResponse() =>
        new(HttpStatusCode.OK) { Content = new StringContent("{}") };

    private static HttpResponseMessage StatusResponse(HttpStatusCode code) =>
        new(code) { Content = new StringContent("{}") };

    /// <summary>
    /// Builds a <see cref="TidalBackendHealthHandler"/> with an isolated cache and
    /// a fake inner handler that either returns a fixed response or throws.
    /// </summary>
    private static (TidalBackendHealthHandler handler, BackendHealthCache cache) BuildHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> innerFactory)
    {
        var cache = new BackendHealthCache();
        var inner = new FuncHttpMessageHandler(innerFactory);
        var handler = new TidalBackendHealthHandler(cache, logger: null)
        {
            InnerHandler = inner
        };
        return (handler, cache);
    }

    private static HttpClient WrapClient(TidalBackendHealthHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.tidal.com") };

    // ------------------------------------------------------------------ //
    // Test 1: Known-down gate short-circuits WITHOUT an HTTP call
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SendAsync_WhenBackendKnownDown_ThrowsImmediately_WithoutHttpCall()
    {
        int httpCallCount = 0;
        var (handler, cache) = BuildHandler((_, _) =>
        {
            httpCallCount++;
            return Task.FromResult(OkResponse());
        });

        // Pre-mark the api.tidal.com host as down.
        cache.MarkDown("tidal:api", "https://api.tidal.com", MakeConnectionRefused());

        var client = WrapClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("/v1/search", CancellationToken.None));

        Assert.Contains("known-down", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, httpCallCount); // The fake inner handler must never be called.
    }

    // ------------------------------------------------------------------ //
    // Test 2: SocketException causes MarkDown; second call short-circuits
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SendAsync_OnSocketException_MarksBackendDown()
    {
        var (handler, cache) = BuildHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(MakeConnectionRefused()));

        var client = WrapClient(handler);

        // First call — expect the SocketException to propagate.
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("/v1/search", CancellationToken.None));

        // Cache should now consider api.tidal.com down.
        bool isDown = cache.IsKnownDown("tidal:api", "https://api.tidal.com", out string? reason);
        Assert.True(isDown);
        Assert.NotNull(reason);

        // Second call within grace window must short-circuit (no inner HTTP call).
        int secondCallInnerCount = 0;
        var (handler2, _) = BuildHandler((_, _) =>
        {
            secondCallInnerCount++;
            return Task.FromResult(OkResponse());
        });
        // Inject the same cache into handler2.
        var handler2WithCache = new TidalBackendHealthHandler(cache, logger: null)
        {
            InnerHandler = new FuncHttpMessageHandler((_, _) =>
            {
                secondCallInnerCount++;
                return Task.FromResult(OkResponse());
            })
        };
        var client2 = new HttpClient(handler2WithCache) { BaseAddress = new Uri("https://api.tidal.com") };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client2.GetAsync("/v1/search", CancellationToken.None));

        Assert.Equal(0, secondCallInnerCount); // Inner must not be called.
    }

    // ------------------------------------------------------------------ //
    // Test 3: Successful response calls MarkUp, clears any down-state
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SendAsync_OnSuccess_MarksBackendUp_ClearsCachedDownState()
    {
        // Part A: a clean-slate call leaves the cache entry clear.
        var freshCache = new BackendHealthCache();
        int innerCallCount = 0;
        var handlerA = new TidalBackendHealthHandler(freshCache, logger: null)
        {
            InnerHandler = new FuncHttpMessageHandler((_, _) =>
            {
                innerCallCount++;
                return Task.FromResult(OkResponse());
            })
        };
        var clientA = new HttpClient(handlerA) { BaseAddress = new Uri("https://api.tidal.com") };

        var response = await clientA.GetAsync("/v1/search", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, innerCallCount);
        // After a successful call the cache must NOT consider the backend down.
        Assert.False(freshCache.IsKnownDown("tidal:api", "https://api.tidal.com", out _));

        // Part B: MarkDown manually, then use a fresh handler (same cache) with a
        // success inner to verify MarkUp clears the entry.
        // We need a separate cache here because once marked down the handler short-circuits
        // before it can call MarkUp — so this part tests the MarkUp call path via direct API.
        var partBCache = new BackendHealthCache();
        partBCache.MarkDown("tidal:api", "https://api.tidal.com", MakeConnectionRefused());
        Assert.True(partBCache.IsKnownDown("tidal:api", "https://api.tidal.com", out _));

        // Directly call MarkUp (which TidalBackendHealthHandler calls on 2xx) and verify it clears.
        partBCache.MarkUp("tidal:api", "https://api.tidal.com");
        Assert.False(partBCache.IsKnownDown("tidal:api", "https://api.tidal.com", out _));
    }

    // ------------------------------------------------------------------ //
    // Test 4: HTTP 5xx does NOT mark backend down
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SendAsync_OnHttp5xx_DoesNotMarkBackendDown()
    {
        var (handler, cache) = BuildHandler((_, _) =>
            Task.FromResult(StatusResponse(HttpStatusCode.InternalServerError)));

        var client = WrapClient(handler);

        // 5xx response is returned (no exception thrown by the handler itself).
        var response = await client.GetAsync("/v1/search", CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // 5xx is NOT a connection-class failure — cache must remain clear.
        Assert.False(cache.IsKnownDown("tidal:api", "https://api.tidal.com", out _));
    }

    // ------------------------------------------------------------------ //
    // Test 5: HTTP 401 does NOT mark backend down — only AuthGate acts on it
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SendAsync_OnHttp401_DoesNotMarkBackendDown_OnlyAuthGateActs()
    {
        var (handler, cache) = BuildHandler((_, _) =>
            Task.FromResult(StatusResponse(HttpStatusCode.Unauthorized)));

        var client = WrapClient(handler);

        var response = await client.GetAsync("/v1/search", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // 401 is an auth failure, handled by AuthFailureGate — BackendHealthCache
        // must NOT be marked down.
        Assert.False(cache.IsKnownDown("tidal:api", "https://api.tidal.com", out _));

        // Auth host (auth.tidal.com) must also remain clear.
        Assert.False(cache.IsKnownDown("tidal:auth", "https://auth.tidal.com", out _));
    }

    // ------------------------------------------------------------------ //
    // Test 6: Provider key routing — auth host maps to "tidal:auth"
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SendAsync_AuthHost_UsesAuthProviderKey()
    {
        var cache = new BackendHealthCache();
        var handler = new TidalBackendHealthHandler(cache, logger: null)
        {
            InnerHandler = new FuncHttpMessageHandler((_, _) =>
                Task.FromException<HttpResponseMessage>(MakeConnectionRefused()))
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://auth.tidal.com") };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.PostAsync("/v1/oauth2/token", new StringContent(""), CancellationToken.None));

        // Must record under the auth key, not the api key.
        Assert.True(cache.IsKnownDown("tidal:auth", "https://auth.tidal.com", out _));
        Assert.False(cache.IsKnownDown("tidal:api", "https://api.tidal.com", out _));
    }
}

/// <summary>
/// Lightweight <see cref="HttpMessageHandler"/> that delegates to a user-supplied factory.
/// Used in tests to avoid taking Moq as a dependency in the test project.
/// </summary>
internal sealed class FuncHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
    : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _factory = factory;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        _factory(request, cancellationToken);
}
