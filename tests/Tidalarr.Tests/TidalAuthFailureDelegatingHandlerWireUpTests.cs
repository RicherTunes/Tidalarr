using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Common.Services.Bridge;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Tidalarr.Tests;

/// <summary>
/// Wave 4: proves the AuthFailureDelegatingHandler is wired into Tidal's
/// API-side HttpClient pipelines (TidalApiClient, TidalOrchestrator,
/// TidalChunkDownloader) but NOT into the TidalOAuthService client — the
/// OAuth path must remain ungated so the user can recover the gate via
/// re-auth.
/// </summary>
public sealed class TidalAuthFailureDelegatingHandlerWireUpTests
{
    private sealed class StubPrimaryHandler : HttpMessageHandler
    {
        public HttpStatusCode NextStatus { get; set; } = HttpStatusCode.OK;
        public int CallCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(NextStatus));
        }
    }

    private static (HttpClient Client, DefaultAuthFailureHandler Handler, AuthFailureGate Gate, StubPrimaryHandler Stub)
        BuildApiPipeline()
    {
        // Replay the slice of TidalModule that wires the gate + delegating handler
        // for an API-side HttpClient (e.g. TidalApiClient). The full module pulls
        // in too many Lidarr-host dependencies for a unit test.
        var services = new ServiceCollection();
        services.AddSingleton<IAuthFailureHandler>(new DefaultAuthFailureHandler(NullLogger<DefaultAuthFailureHandler>.Instance));
        services.AddSingleton(sp => new AuthFailureGate(
            sp.GetRequiredService<IAuthFailureHandler>(),
            TimeProvider.System,
            TimeSpan.FromSeconds(60),
            NullLogger<AuthFailureGate>.Instance));
        services.AddTransient<AuthFailureDelegatingHandler>();

        var stub = new StubPrimaryHandler();
        services.AddHttpClient("TidalApi", c => c.BaseAddress = new Uri("https://api.tidal.test/"))
            .AddHttpMessageHandler<AuthFailureDelegatingHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => stub);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("TidalApi");
        var handler = (DefaultAuthFailureHandler)provider.GetRequiredService<IAuthFailureHandler>();
        var gate = provider.GetRequiredService<AuthFailureGate>();
        return (client, handler, gate, stub);
    }

    [Fact]
    public async Task ApiPipeline_On401_LatchesAuthHandler()
    {
        var (client, handler, _, stub) = BuildApiPipeline();
        stub.NextStatus = HttpStatusCode.Unauthorized;

        using var resp = await client.GetAsync("/v1/me/albums");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Equal(AuthStatus.Failed, handler.Status);
    }

    [Fact]
    public async Task ApiPipeline_AfterLatch_ShortCircuitsSubsequentRequests()
    {
        var (client, handler, _, stub) = BuildApiPipeline();
        await handler.HandleFailureAsync(new AuthFailure { Message = "token revoked" });
        stub.NextStatus = HttpStatusCode.Unauthorized;

        // Probe slot consumed by first call.
        using (var probe = await client.GetAsync("/v1/me/albums"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, probe.StatusCode);
        }
        Assert.Equal(1, stub.CallCount);

        for (var i = 0; i < 20; i++)
        {
            await Assert.ThrowsAsync<AuthGatedException>(() => client.GetAsync("/v1/me/albums"));
        }
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task ApiPipeline_OnRecovery_ResumesNormalTraffic()
    {
        var (client, handler, _, stub) = BuildApiPipeline();
        await handler.HandleFailureAsync(new AuthFailure { Message = "bad" });
        stub.NextStatus = HttpStatusCode.OK;

        using (var probe = await client.GetAsync("/v1/me/albums"))
        {
            Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        }
        Assert.Equal(AuthStatus.Authenticated, handler.Status);

        for (var i = 0; i < 5; i++)
        {
            using var r = await client.GetAsync("/v1/me/albums");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
        Assert.Equal(6, stub.CallCount);
    }
}
