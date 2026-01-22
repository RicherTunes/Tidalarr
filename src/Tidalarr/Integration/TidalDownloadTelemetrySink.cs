using Lidarr.Plugin.Common.Services.Download;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Integration;

/// <summary>
/// Logs per-track download telemetry for performance analysis.
/// Emits structured logs (single line per track) for easy parsing.
/// </summary>
public sealed class TidalDownloadTelemetrySink : IDownloadTelemetrySink
{
    private readonly ILogger? _logger;

    public TidalDownloadTelemetrySink(ILogger<TidalDownloadTelemetrySink>? logger = null)
    {
        _logger = logger;
    }

    public void OnTrackCompleted(DownloadTelemetry telemetry)
    {
        if (telemetry.Success)
        {
            _logger?.LogInformation(
                "Download completed: track={TrackId} album={AlbumId} bytes={Bytes} elapsed={Elapsed:F2}s rate={Rate:F1}KB/s retries={Retries} 429s={TooManyRequests}",
                telemetry.TrackId,
                telemetry.AlbumId ?? "unknown",
                telemetry.BytesWritten,
                telemetry.Elapsed.TotalSeconds,
                telemetry.BytesPerSecond / 1024.0,
                telemetry.RetryCount,
                telemetry.TooManyRequestsCount);
        }
        else
        {
            _logger?.LogWarning(
                "Download failed: track={TrackId} album={AlbumId} elapsed={Elapsed:F2}s retries={Retries} 429s={TooManyRequests} error={Error}",
                telemetry.TrackId,
                telemetry.AlbumId ?? "unknown",
                telemetry.Elapsed.TotalSeconds,
                telemetry.RetryCount,
                telemetry.TooManyRequestsCount,
                telemetry.ErrorMessage ?? "unknown");
        }
    }
}
