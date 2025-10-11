using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net;

namespace Tidalarr.Infrastructure.Resilience;

[System.Obsolete("Replaced by Common HttpClientExtensions.ExecuteWithRetryAsync. Retained for reference/tests only; consider removal in next major.")]
public static class TidalResiliencePolicy
{
    public static IAsyncPolicy<HttpResponseMessage> CreateHttpRetryPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutRejectedException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && ShouldRetry(r.StatusCode))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var operation = context.GetValueOrDefault("operation", "unknown");
                    Console.WriteLine($"🔄 Retry {retryCount} for {operation} in {timespan.TotalSeconds}s");
                });
    }
    
    public static IAsyncPolicy CreateTokenRefreshPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase))
            .WaitAndRetryAsync(
                retryCount: 2, // Fewer retries for auth
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(retryAttempt * 2),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"🔐 Auth retry {retryCount} in {timespan.TotalSeconds}s: {exception.Message}");
                });
    }
    
    public static IAsyncPolicy CreateChunkDownloadPolicy()
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 2, // Fast failure for chunks
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500 * retryAttempt),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    var chunkUrl = context.GetValueOrDefault("chunkUrl", "unknown");
                    Console.WriteLine($"⬇️  Chunk retry {retryCount} for {chunkUrl}");
                });
    }
    
    public static IAsyncPolicy<T> CreateCircuitBreakerPolicy<T>()
    {
        return Policy<T>
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (delegateResult, duration) =>
                {
                    var message = delegateResult.Exception?.Message ?? "Unknown error";
                    Console.WriteLine($"🚨 Circuit breaker OPEN for {duration.TotalMinutes} minutes: {message}");
                },
                onReset: () =>
                {
                    Console.WriteLine($"✅ Circuit breaker RESET - service recovered");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine($"⚠️  Circuit breaker HALF-OPEN - testing service");
                });
    }
    
    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => true,  // 429 - Rate limited
            HttpStatusCode.InternalServerError => true,  // 500
            HttpStatusCode.BadGateway => true,  // 502
            HttpStatusCode.ServiceUnavailable => true,  // 503
            HttpStatusCode.GatewayTimeout => true,  // 504
            HttpStatusCode.RequestTimeout => true,  // 408
            _ => false
        };
    }
}

