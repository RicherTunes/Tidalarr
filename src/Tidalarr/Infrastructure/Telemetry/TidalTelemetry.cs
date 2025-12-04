using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Tidalarr.Core.Models;

namespace Tidalarr.Infrastructure.Telemetry;

public class TidalTelemetry(ILogger<TidalTelemetry> logger)
{
    private readonly ILogger<TidalTelemetry> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private static readonly ActivitySource ActivitySource = new("Tidalarr");

    // Structured logging for downloads
    public void TrackDownloadStarted(string trackId, TidalQuality quality)
    {
        this._logger.LogInformation("Download started for track {TrackId} at quality {Quality}",
            trackId, quality);
    }

    public void TrackDownloadCompleted(string trackId, TidalQuality quality, TimeSpan duration, long fileSize)
    {
        this._logger.LogInformation("Download completed for track {TrackId}. Quality: {Quality}, Duration: {Duration}ms, Size: {FileSize} bytes",
            trackId, quality, duration.TotalMilliseconds, fileSize);
    }

    public void TrackDownloadFailed(string trackId, TidalQuality quality, Exception exception)
    {
        this._logger.LogError(exception, "Download failed for track {TrackId} at quality {Quality}",
            trackId, quality);
    }

    // API call tracking
    public void TrackApiCall(string endpoint, int statusCode, TimeSpan latency)
    {
        this._logger.LogDebug("API call to {Endpoint} returned {StatusCode} in {Latency}ms",
            endpoint, statusCode, latency.TotalMilliseconds);
    }

    // Authentication tracking
    public void TrackAuthentication(bool success, string? errorMessage = null)
    {
        if (success)
        {
            this._logger.LogInformation("Tidal authentication successful");
        }
        else
        {
            this._logger.LogWarning("Tidal authentication failed: {Error}", errorMessage);
        }
    }

    // Search tracking
    public void TrackSearch(string query, int resultCount, TimeSpan duration)
    {
        this._logger.LogInformation("Search for '{Query}' returned {ResultCount} results in {Duration}ms",
            query, resultCount, duration.TotalMilliseconds);
    }

    // Performance monitoring
    public IDisposable StartActivity(string name)
    {
        Activity? activity = ActivitySource.StartActivity(name);
        return activity ?? (IDisposable)new NullActivity();
    }

    // Circuit breaker events
    public void CircuitBreakerOpened(string service, Exception exception)
    {
        this._logger.LogWarning("Circuit breaker OPENED for {Service}: {Error}",
            service, exception.Message);
    }

    public void CircuitBreakerClosed(string service)
    {
        this._logger.LogInformation("Circuit breaker CLOSED for {Service} - service recovered",
            service);
    }

    // Error correlation
    public void TrackCorrelatedError(string correlationId, string operation, Exception exception)
    {
        this._logger.LogError(exception, "Operation {Operation} failed (ID: {CorrelationId})",
            operation, correlationId);
    }
}

public class NullActivity : IDisposable
{
    public void Dispose() { }
}

