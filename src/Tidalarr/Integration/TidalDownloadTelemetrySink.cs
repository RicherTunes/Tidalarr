using Lidarr.Plugin.Common.Services.Download;
using NLog;

namespace Tidalarr.Integration;

/// <summary>
/// Logs per-track download telemetry for performance analysis.
/// Emits structured logs (single line per track) for easy parsing.
/// </summary>
public sealed class TidalDownloadTelemetrySink : IDownloadTelemetrySink
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public void OnTrackCompleted(DownloadTelemetry telemetry)
    {
        if (telemetry.Success)
        {
            Logger.Info(
                "Tidalarr track completed: track={0} album={1} bytes={2} elapsed={3:F2}s rate={4:F1}KB/s retries={5} 429s={6}",
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
            Logger.Warn(
                "Tidalarr track failed: track={0} album={1} elapsed={2:F2}s retries={3} 429s={4} error={5}",
                telemetry.TrackId,
                telemetry.AlbumId ?? "unknown",
                telemetry.Elapsed.TotalSeconds,
                telemetry.RetryCount,
                telemetry.TooManyRequestsCount,
                telemetry.ErrorMessage ?? "unknown");
        }
    }
}
