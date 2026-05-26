using Lidarr.Plugin.Common.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Tests for <see cref="LoggerExtensions"/> observability methods.
/// The former ObservabilityShim (reflection-based workaround for the cross-ALC type-lookup bug)
/// has been replaced with direct calls now that ILRepack inlines Common into the plugin DLL.
/// These tests verify the direct call path that TidalApiClient now uses.
/// </summary>
public class ObservabilityShimTests
{
    [Fact]
    public void LogApiCallStarted_ReturnsDisposable()
    {
        ILogger logger = NullLogger.Instance;
        using IDisposable d = logger.LogApiCallStarted(service: "tidal", endpoint: "search");
        Assert.NotNull(d);
    }

    [Fact]
    public void LogApiCallStarted_NullLogger_Throws()
    {
        // Null logger is not guarded by the extension method — callers should not pass null.
        // This matches the contract: NullLogger is the safe no-op, not null.
        Assert.Throws<ArgumentNullException>(() =>
        {
            ILogger? logger = null;
            logger!.LogApiCallStarted(service: "tidal", endpoint: "search");
        });
    }

    [Fact]
    public void LogApiCallStarted_WithCorrelationId_ReturnsDisposable()
    {
        ILogger logger = NullLogger.Instance;
        using IDisposable d = logger.LogApiCallStarted(service: "tidal", endpoint: "albums", correlationId: "corr-1");
        Assert.NotNull(d);
    }

    [Fact]
    public void LogApiCallCompleted_NullLogger_DoesNotThrow_ViaCapturing()
    {
        // LogApiCallCompleted writes to the current Activity context + the logger.
        // With NullLogger it is a no-op and must not throw.
        CapturingLogger logger = new();
        Exception? ex = Record.Exception(() =>
            logger.LogApiCallCompleted(service: "tidal", endpoint: "search", statusCode: 200, success: true, duration: TimeSpan.FromMilliseconds(10)));
        Assert.Null(ex);
    }

    [Fact]
    public void LogApiCallStarted_EmitsStartedEntry()
    {
        CapturingLogger logger = new();
        using (IDisposable scope = logger.LogApiCallStarted(service: "tidal", endpoint: "search", correlationId: "corr-1"))
        {
            Assert.NotNull(scope);
            Assert.Contains(logger.Entries, e =>
                e.Level == LogLevel.Information &&
                e.Message.Contains("API call started", StringComparison.Ordinal) &&
                e.Message.Contains("tidal", StringComparison.Ordinal) &&
                e.Message.Contains("search", StringComparison.Ordinal));
        }

        // Disposing the scope emits the "finished" entry inside Common's ActivityScope.
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("API call finished", StringComparison.Ordinal));
    }

    [Fact]
    public void LogApiCallCompleted_EmitsCompletedEntry()
    {
        CapturingLogger logger = new();
        logger.LogApiCallCompleted(service: "tidal", endpoint: "track", statusCode: 200, success: true, duration: TimeSpan.FromMilliseconds(42));

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("API call completed", StringComparison.Ordinal) &&
            e.Message.Contains("tidal", StringComparison.Ordinal) &&
            e.Message.Contains("track", StringComparison.Ordinal));
    }

    [Fact]
    public void LogApiCallStarted_RepeatedInvocations_AllSucceed()
    {
        CapturingLogger logger = new();
        for (int i = 0; i < 3; i++)
        {
            using (logger.LogApiCallStarted(service: "tidal", endpoint: "ep" + i)) { }
            logger.LogApiCallCompleted(service: "tidal", endpoint: "ep" + i, statusCode: 200, success: true, duration: TimeSpan.FromMilliseconds(i));
        }

        // 3 starts + 3 finishes (from disposal) + 3 completes => at least 9 Information entries
        int infoCount = logger.Entries.Count(e => e.Level == LogLevel.Information);
        Assert.True(infoCount >= 9, $"Expected at least 9 Information entries, got {infoCount}");
    }

    [Fact]
    public void LogApiCallStarted_ScopeIsNotNoop_DisposalEmitsLog()
    {
        CapturingLogger logger = new();
        using IDisposable scope = logger.LogApiCallStarted(service: "tidal", endpoint: "albums");
        Assert.NotNull(scope);
        int beforeDispose = logger.Entries.Count;
        scope.Dispose();
        Assert.True(logger.Entries.Count > beforeDispose,
            "Disposing the ActivityScope should emit a log entry");
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
