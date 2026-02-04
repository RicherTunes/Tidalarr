using Microsoft.Extensions.Logging;

namespace Tidalarr.Infrastructure.Logging;

/// <summary>
/// Structured logging extensions for Tidalarr services using source-generated LoggerMessage.
/// Provides consistent, parseable log output with correlation IDs.
/// </summary>
internal static partial class TidalarrLoggerExtensions
{
    private const string PluginName = "Tidalarr";

    #region Request Lifecycle (EventIds 3000-3009)

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "[{Plugin}] {Service} {Operation} started | CorrelationId={CorrelationId} Context={Context} Attempt={Attempt}")]
    public static partial void LogRequestStart(
        this ILogger logger,
        string plugin,
        string service,
        string operation,
        string correlationId,
        string context,
        int attempt);

    public static void LogRequestStart(
        this ILogger logger,
        string service,
        string operation,
        string correlationId,
        string? context = null,
        int attempt = 1)
    {
        LogRequestStart(logger, PluginName, service, operation, correlationId, context ?? "default", attempt);
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "[{Plugin}] {Service} {Operation} completed | CorrelationId={CorrelationId} ElapsedMs={ElapsedMs} ItemCount={ItemCount}")]
    public static partial void LogRequestComplete(
        this ILogger logger,
        string plugin,
        string service,
        string operation,
        string correlationId,
        long elapsedMs,
        int itemCount);

    public static void LogRequestComplete(
        this ILogger logger,
        string service,
        string operation,
        string correlationId,
        long elapsedMs,
        int? itemCount = null)
    {
        LogRequestComplete(logger, PluginName, service, operation, correlationId, elapsedMs, itemCount ?? 0);
    }

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Error,
        Message = "[{Plugin}] {Service} {Operation} error | CorrelationId={CorrelationId} ErrorCode={ErrorCode} Error={Error}")]
    public static partial void LogRequestError(
        this ILogger logger,
        string plugin,
        string service,
        string operation,
        string correlationId,
        string errorCode,
        string error);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Error,
        Message = "[{Plugin}] {Service} {Operation} error | CorrelationId={CorrelationId} ErrorCode={ErrorCode} Error={Error}")]
    public static partial void LogRequestErrorWithException(
        this ILogger logger,
        Exception exception,
        string plugin,
        string service,
        string operation,
        string correlationId,
        string errorCode,
        string error);

    public static void LogRequestError(
        this ILogger logger,
        string service,
        string operation,
        string correlationId,
        string errorCode,
        string errorMessage,
        Exception? exception = null)
    {
        string redacted = RedactSensitive(errorMessage);
        if (exception != null)
        {
            LogRequestErrorWithException(logger, exception, PluginName, service, operation, correlationId, errorCode, redacted);
        }
        else
        {
            LogRequestError(logger, PluginName, service, operation, correlationId, errorCode, redacted);
        }
    }

    #endregion

    #region Authentication (EventIds 3010-3019)

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Information,
        Message = "[{Plugin}] Authentication succeeded | CorrelationId={CorrelationId}")]
    public static partial void LogAuthSuccess(
        this ILogger logger,
        string plugin,
        string correlationId);

    public static void LogAuthSuccess(this ILogger logger, string correlationId)
    {
        LogAuthSuccess(logger, PluginName, correlationId);
    }

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Warning,
        Message = "[{Plugin}] Authentication failed | CorrelationId={CorrelationId} Reason={Reason}")]
    public static partial void LogAuthFail(
        this ILogger logger,
        string plugin,
        string correlationId,
        string reason);

    public static void LogAuthFail(this ILogger logger, string correlationId, string reason)
    {
        LogAuthFail(logger, PluginName, correlationId, RedactSensitive(reason));
    }

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Information,
        Message = "[{Plugin}] Token refresh succeeded | CorrelationId={CorrelationId} ExpiresInMinutes={ExpiresInMinutes}")]
    public static partial void LogTokenRefreshSuccess(
        this ILogger logger,
        string plugin,
        string correlationId,
        double expiresInMinutes);

    public static void LogTokenRefreshSuccess(this ILogger logger, string correlationId, TimeSpan? expiresIn = null)
    {
        LogTokenRefreshSuccess(logger, PluginName, correlationId, expiresIn?.TotalMinutes ?? -1);
    }

    [LoggerMessage(
        EventId = 3013,
        Level = LogLevel.Warning,
        Message = "[{Plugin}] Token refresh failed | CorrelationId={CorrelationId} Reason={Reason}")]
    public static partial void LogTokenRefreshFail(
        this ILogger logger,
        string plugin,
        string correlationId,
        string reason);

    public static void LogTokenRefreshFail(this ILogger logger, string correlationId, string reason)
    {
        LogTokenRefreshFail(logger, PluginName, correlationId, RedactSensitive(reason));
    }

    #endregion

    #region Rate Limiting (EventIds 3020-3029)

    [LoggerMessage(
        EventId = 3020,
        Level = LogLevel.Warning,
        Message = "[{Plugin}] {Service} rate limited | CorrelationId={CorrelationId} RetryAfterMs={RetryAfterMs}")]
    public static partial void LogRateLimited(
        this ILogger logger,
        string plugin,
        string service,
        string correlationId,
        double retryAfterMs);

    public static void LogRateLimited(this ILogger logger, string service, string correlationId, TimeSpan? retryAfter = null)
    {
        LogRateLimited(logger, PluginName, service, correlationId, retryAfter?.TotalMilliseconds ?? -1);
    }

    [LoggerMessage(
        EventId = 3021,
        Level = LogLevel.Information,
        Message = "[{Plugin}] {Service} rate limit recovered | CorrelationId={CorrelationId} TotalAttempts={TotalAttempts}")]
    public static partial void LogRateLimitRecovered(
        this ILogger logger,
        string plugin,
        string service,
        string correlationId,
        int totalAttempts);

    public static void LogRateLimitRecovered(this ILogger logger, string service, string correlationId, int totalAttempts)
    {
        LogRateLimitRecovered(logger, PluginName, service, correlationId, totalAttempts);
    }

    #endregion

    #region Download Operations (EventIds 3030-3039)

    [LoggerMessage(
        EventId = 3030,
        Level = LogLevel.Information,
        Message = "[{Plugin}] Download started | CorrelationId={CorrelationId} AlbumId={AlbumId} TrackCount={TrackCount} Quality={Quality}")]
    public static partial void LogDownloadStart(
        this ILogger logger,
        string plugin,
        string correlationId,
        string albumId,
        int trackCount,
        string quality);

    public static void LogDownloadStart(this ILogger logger, string correlationId, string albumId, int trackCount, string? quality = null)
    {
        LogDownloadStart(logger, PluginName, correlationId, albumId, trackCount, quality ?? "default");
    }

    [LoggerMessage(
        EventId = 3031,
        Level = LogLevel.Information,
        Message = "[{Plugin}] Download completed | CorrelationId={CorrelationId} AlbumId={AlbumId} Success={Success} Failed={Failed} ElapsedMs={ElapsedMs}")]
    public static partial void LogDownloadComplete(
        this ILogger logger,
        string plugin,
        string correlationId,
        string albumId,
        int success,
        int failed,
        long elapsedMs);

    public static void LogDownloadComplete(this ILogger logger, string correlationId, string albumId, int successCount, int failCount, long elapsedMs)
    {
        LogDownloadComplete(logger, PluginName, correlationId, albumId, successCount, failCount, elapsedMs);
    }

    [LoggerMessage(
        EventId = 3032,
        Level = LogLevel.Debug,
        Message = "[{Plugin}] Track progress | CorrelationId={CorrelationId} TrackId={TrackId} Progress={Progress}/{Total} Status={Status}")]
    public static partial void LogTrackProgress(
        this ILogger logger,
        string plugin,
        string correlationId,
        string trackId,
        int progress,
        int total,
        string status);

    public static void LogTrackProgress(this ILogger logger, string correlationId, string trackId, int trackNumber, int totalTracks, string status)
    {
        LogTrackProgress(logger, PluginName, correlationId, trackId, trackNumber, totalTracks, status);
    }

    [LoggerMessage(
        EventId = 3033,
        Level = LogLevel.Debug,
        Message = "[{Plugin}] Chunk downloaded | CorrelationId={CorrelationId} ChunkIndex={ChunkIndex} TotalChunks={TotalChunks} BytesReceived={BytesReceived}")]
    public static partial void LogChunkDownloaded(
        this ILogger logger,
        string plugin,
        string correlationId,
        int chunkIndex,
        int totalChunks,
        long bytesReceived);

    public static void LogChunkDownloaded(this ILogger logger, string correlationId, int chunkIndex, int totalChunks, long bytesReceived)
    {
        LogChunkDownloaded(logger, PluginName, correlationId, chunkIndex, totalChunks, bytesReceived);
    }

    #endregion

    #region Health Checks (EventIds 3040-3049)

    [LoggerMessage(
        EventId = 3040,
        Level = LogLevel.Information,
        Message = "[{Plugin}] {Service} health check passed | ElapsedMs={ElapsedMs}")]
    public static partial void LogHealthCheckPass(
        this ILogger logger,
        string plugin,
        string service,
        long elapsedMs);

    public static void LogHealthCheckPass(this ILogger logger, string service, long elapsedMs)
    {
        LogHealthCheckPass(logger, PluginName, service, elapsedMs);
    }

    [LoggerMessage(
        EventId = 3041,
        Level = LogLevel.Warning,
        Message = "[{Plugin}] {Service} health check failed | Reason={Reason}")]
    public static partial void LogHealthCheckFail(
        this ILogger logger,
        string plugin,
        string service,
        string reason);

    public static void LogHealthCheckFail(this ILogger logger, string service, string reason)
    {
        LogHealthCheckFail(logger, PluginName, service, RedactSensitive(reason));
    }

    #endregion

    #region API Operations (EventIds 3050-3059)

    [LoggerMessage(
        EventId = 3050,
        Level = LogLevel.Debug,
        Message = "[{Plugin}] API {Method} {Endpoint} | CorrelationId={CorrelationId}")]
    public static partial void LogApiCallStart(
        this ILogger logger,
        string plugin,
        string method,
        string endpoint,
        string correlationId);

    public static void LogApiCallStart(this ILogger logger, string endpoint, string correlationId, string? method = null)
    {
        LogApiCallStart(logger, PluginName, method ?? "GET", endpoint, correlationId);
    }

    [LoggerMessage(
        EventId = 3051,
        Level = LogLevel.Debug,
        Message = "[{Plugin}] API response | CorrelationId={CorrelationId} Endpoint={Endpoint} StatusCode={StatusCode} ElapsedMs={ElapsedMs}")]
    public static partial void LogApiCallCompleteDebug(
        this ILogger logger,
        string plugin,
        string correlationId,
        string endpoint,
        int statusCode,
        long elapsedMs);

    [LoggerMessage(
        EventId = 3052,
        Level = LogLevel.Warning,
        Message = "[{Plugin}] API response | CorrelationId={CorrelationId} Endpoint={Endpoint} StatusCode={StatusCode} ElapsedMs={ElapsedMs}")]
    public static partial void LogApiCallCompleteWarn(
        this ILogger logger,
        string plugin,
        string correlationId,
        string endpoint,
        int statusCode,
        long elapsedMs);

    public static void LogApiCallComplete(this ILogger logger, string endpoint, string correlationId, int statusCode, long elapsedMs)
    {
        if (statusCode >= 400)
        {
            LogApiCallCompleteWarn(logger, PluginName, correlationId, endpoint, statusCode, elapsedMs);
        }
        else
        {
            LogApiCallCompleteDebug(logger, PluginName, correlationId, endpoint, statusCode, elapsedMs);
        }
    }

    #endregion

    #region Search Operations (EventIds 3060-3069)

    [LoggerMessage(
        EventId = 3060,
        Level = LogLevel.Information,
        Message = "[{Plugin}] Search completed | CorrelationId={CorrelationId} Query={Query} Results={Results} ElapsedMs={ElapsedMs}")]
    public static partial void LogSearch(
        this ILogger logger,
        string plugin,
        string correlationId,
        string query,
        int results,
        long elapsedMs);

    public static void LogSearch(this ILogger logger, string correlationId, string query, int resultCount, long elapsedMs)
    {
        LogSearch(logger, PluginName, correlationId, TruncateQuery(query), resultCount, elapsedMs);
    }

    #endregion

    #region Utility Methods (EventIds 3070-3079)

    [LoggerMessage(
        EventId = 3070,
        Level = LogLevel.Information,
        Message = "{Message} | CorrelationId={CorrelationId}")]
    public static partial void InfoWithCorrelation(
        this ILogger logger,
        string message,
        string correlationId);

    [LoggerMessage(
        EventId = 3071,
        Level = LogLevel.Debug,
        Message = "{Message} | CorrelationId={CorrelationId}")]
    public static partial void DebugWithCorrelation(
        this ILogger logger,
        string message,
        string correlationId);

    #endregion

    #region Redaction Helpers

    private static string RedactSensitive(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        if (value.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
            value.Length > 100)
        {
            if (value.Length > 100)
            {
                return value[..50] + "...[REDACTED]";
            }
        }

        return value;
    }

    private static string TruncateQuery(string query)
    {
        return string.IsNullOrEmpty(query) ? "[empty]" : query.Length > 50 ? query[..47] + "..." : query;
    }

    #endregion
}
