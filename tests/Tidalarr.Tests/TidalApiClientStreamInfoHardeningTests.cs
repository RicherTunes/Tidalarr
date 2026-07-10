using System.Net;
using System.Text;
using Lidarr.Plugin.Abstractions.Contracts;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

/// <summary>
/// 429-retry-exhaustion parity for every <see cref="TidalApiClient"/> endpoint that routes through
/// Common's <c>ExecuteWithRetryAsync</c> + <c>ReportRateLimitStatusAsync</c>: the retry helper
/// disposes throttled responses and surfaces exhaustion as an <see cref="HttpRequestException"/>,
/// so without a filtered catch a persistent 429 never reaches the rate-limit reporter. Mirrors the
/// <c>GetPlaybackInfoAsync</c> hardening in <see cref="TidalApiClientPlaybackInfoHardeningTests"/>.
/// </summary>
public class TidalApiClientStreamInfoHardeningTests
{
    private sealed class AlwaysTooManyRequestsHandler : HttpMessageHandler
    {
        private int _requestCount;
        public int RequestCount => this._requestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref this._requestCount);
            HttpResponseMessage msg = new(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }

    // Self-contained auth stub: the shared MockAuth lives in TidalApiClientErrorTests.cs, which the
    // broad Tidal*.cs Compile Remove excludes under ExcludeHostBridge=true while this file is re-included.
    private sealed class StubAuth : ITidalAuth
    {
        public bool IsAuthenticated => true;

        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
        {
            return Task.FromResult(new TidalAuthUrl("u", "v", "s", string.Empty));
        }

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
        {
            return Task.FromResult(Default());
        }

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
        {
            return Task.FromResult(Default());
        }

        public Task<TidalTokens> GetValidTokensAsync()
        {
            return Task.FromResult(Default());
        }

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
        {
            return TidalCallbackResult.Failure("Not implemented in test stub");
        }

        private static TidalTokens Default()
        {
            return new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
        }
    }

    private sealed class RecordingRateLimitReporter : IRateLimitReporter
    {
        public int RateLimitReports { get; private set; }
        public TimeSpan? LastRetryAfter { get; private set; }
        public RateLimitStatus Status { get; } = new() { IsRateLimited = false };

        public ValueTask ReportRateLimitAsync(TimeSpan retryAfter, CancellationToken cancellationToken = default)
        {
            RateLimitReports++;
            LastRetryAfter = retryAfter;
            return ValueTask.CompletedTask;
        }

        public ValueTask ReportRateLimitClearedAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ReportBackoffAsync(TimeSpan delay, string reason, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    public static TheoryData<string> Endpoints => new()
    {
        nameof(TidalApiClient.GetStreamInfoAsync),
        nameof(TidalApiClient.GetTrackAsync),
        nameof(TidalApiClient.GetAlbumAsync),
        nameof(TidalApiClient.GetAlbumTracksAsync),
        nameof(TidalApiClient.SearchAsync),
        nameof(TidalApiClient.GetFavoriteAlbumsAsync),
    };

    private static Task InvokeEndpoint(TidalApiClient client, string endpoint)
    {
        return endpoint switch
        {
            nameof(TidalApiClient.GetStreamInfoAsync) => client.GetStreamInfoAsync("t1", TidalQuality.Lossless),
            nameof(TidalApiClient.GetTrackAsync) => client.GetTrackAsync("t1"),
            nameof(TidalApiClient.GetAlbumAsync) => client.GetAlbumAsync("a1"),
            nameof(TidalApiClient.GetAlbumTracksAsync) => client.GetAlbumTracksAsync("a1"),
            nameof(TidalApiClient.SearchAsync) => client.SearchAsync("query"),
            nameof(TidalApiClient.GetFavoriteAlbumsAsync) => client.GetFavoriteAlbumsAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, "Unknown endpoint under test"),
        };
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task PersistentRateLimit_ReportsToRateLimitReporter(string endpoint)
    {
        AlwaysTooManyRequestsHandler handler = new();
        RecordingRateLimitReporter reporter = new();
        TidalApiClient client = new(new HttpClient(handler), new StubAuth(), manifestParser: null!, rateLimitReporter: reporter);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => InvokeEndpoint(client, endpoint));

        Assert.True(reporter.RateLimitReports >= 1, $"{endpoint}: a persistent 429 must feed the rate-limit reporter");
        Assert.True(reporter.LastRetryAfter > TimeSpan.Zero, $"{endpoint}: the reported backoff must be positive");
    }
}
