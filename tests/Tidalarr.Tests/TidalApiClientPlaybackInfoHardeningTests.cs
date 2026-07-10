using System.Net;
using System.Text;
using System.Text.Json;
using Lidarr.Plugin.Abstractions.Contracts;
using Tidalarr.Core.Exceptions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

/// <summary>
/// Hardening parity for <see cref="TidalApiClient.GetPlaybackInfoAsync"/> with its sibling
/// <see cref="TidalApiClient.GetStreamInfoAsync"/>: transient failures are retried, 429s feed the
/// rate-limit reporter, and a permanent-restriction 404 still classifies WITHOUT being retried.
/// </summary>
public class TidalApiClientPlaybackInfoHardeningTests
{
    private sealed class SequenceHandler(params (HttpStatusCode Code, string Body)[] responses) : HttpMessageHandler
    {
        private readonly (HttpStatusCode Code, string Body)[] _responses = responses;
        private int _requestCount;
        public int RequestCount => this._requestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int index = Interlocked.Increment(ref this._requestCount) - 1;
            (HttpStatusCode code, string body) = this._responses[Math.Min(index, this._responses.Length - 1)];
            HttpResponseMessage msg = new(code);
            if (!string.IsNullOrEmpty(body))
            {
                msg.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
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

    private static string ValidPlaybackJson()
    {
        TidalPlaybackInfoDto dto = new(
            manifest: Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"urls":["https://cdn.example.com/c1.m4a"]}""")),
            manifestMimeType: "application/vnd.tidal.bts",
            encryptionType: "NONE",
            securityToken: null);
        return JsonSerializer.Serialize(dto);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_TransientServerErrorThenSuccess_RetriesToSuccess()
    {
        SequenceHandler handler = new(
            (HttpStatusCode.ServiceUnavailable, ""),
            (HttpStatusCode.OK, ValidPlaybackJson()));
        TidalApiClient client = new(new HttpClient(handler), new StubAuth());

        TidalPlaybackInfoDto dto = await client.GetPlaybackInfoAsync("t1", TidalQuality.Lossless);

        Assert.Equal("application/vnd.tidal.bts", dto.manifestMimeType);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_PersistentRateLimit_ReportsToRateLimitReporter()
    {
        SequenceHandler handler = new((HttpStatusCode.TooManyRequests, ""));
        RecordingRateLimitReporter reporter = new();
        TidalApiClient client = new(new HttpClient(handler), new StubAuth(), manifestParser: null, rateLimitReporter: reporter);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetPlaybackInfoAsync("t1", TidalQuality.Lossless));

        Assert.True(reporter.RateLimitReports >= 1, "a 429 must feed the rate-limit reporter");
        Assert.True(reporter.LastRetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task GetPlaybackInfoAsync_PermanentRestriction404_ThrowsClassified_WithoutRetry()
    {
        SequenceHandler handler = new((HttpStatusCode.NotFound, """{"subStatus":2001,"userMessage":"Asset is not available"}"""));
        TidalApiClient client = new(new HttpClient(handler), new StubAuth());

        TidalStreamUnavailableException ex = await Assert.ThrowsAsync<TidalStreamUnavailableException>(
            () => client.GetPlaybackInfoAsync("t1", TidalQuality.Lossless));

        Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, ex.Reason);
        // Permanent unavailability must never be retried.
        Assert.Equal(1, handler.RequestCount);
    }
}
