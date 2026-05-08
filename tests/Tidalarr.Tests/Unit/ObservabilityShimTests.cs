using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Infrastructure.Observability;

namespace Tidalarr.Tests.Unit;

public class ObservabilityShimTests
{
    [Fact]
    public void StartApi_NoFlag_NoThrow_Noops()
    {
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", null);
            using IDisposable d = ObservabilityShim.StartApi(NullLogger.Instance, "tidal", "search");
            // If we got here, it didn't throw, which is expected when disabled
            Assert.NotNull(d);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    [Fact]
    public void CompleteApi_NoHelpers_NoThrow()
    {
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", "1");
            ObservabilityShim.CompleteApi(NullLogger.Instance, "tidal", "search", 200, true, TimeSpan.FromMilliseconds(1));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    [Fact]
    public void StartApi_NullLogger_NoFlag_ReturnsNoopDisposable()
    {
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", null);
            using IDisposable d = ObservabilityShim.StartApi(null!, "tidal", "search");
            Assert.NotNull(d);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    [Fact]
    public void StartApi_FlagOn_NullLogger_ShortCircuitsBeforeReflection()
    {
        // The (!IsEnabled || logger == null) guard should prevent any reflection
        // even when the flag is on, if the logger is null.
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", "1");
            using IDisposable d = ObservabilityShim.StartApi(null!, "tidal", "search");
            Assert.NotNull(d);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    [Fact]
    public void CompleteApi_FlagOn_NullLogger_NoThrow()
    {
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", "1");
            ObservabilityShim.CompleteApi(null!, "tidal", "search", 500, false, TimeSpan.Zero);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    /// <summary>
    /// Force-load the common assembly so <c>Type.GetType("...,Lidarr.Plugin.Common")</c> succeeds
    /// in the test AppDomain. The csproj already references it, but a static type touch guarantees
    /// the assembly is loaded before <see cref="Type.GetType(string)"/> resolves it.
    /// </summary>
    private static void EnsureCommonLoaded()
    {
        // Static touch — references the type so the JIT/loader pulls in the assembly.
        _ = typeof(Lidarr.Plugin.Common.Observability.LoggerExtensions).FullName;
    }

    [Fact]
    public void StartApi_FlagOn_CommonReachable_BindsAndInvokesReflectively()
    {
        EnsureCommonLoaded();
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", "1");
            CapturingLogger logger = new();

            using (IDisposable scope = ObservabilityShim.StartApi(logger, "tidal", "search", "corr-1"))
            {
                Assert.NotNull(scope);
                // The reflectively invoked LoggerExtensions.LogApiCallStarted writes a
                // structured Information entry — its presence proves the reflective
                // (env-var → Type.GetType → MethodInfo.Invoke) path executed end-to-end.
                Assert.Contains(logger.Entries, e =>
                    e.Level == LogLevel.Information &&
                    e.Message.Contains("API call started", StringComparison.Ordinal) &&
                    e.Message.Contains("tidal", StringComparison.Ordinal) &&
                    e.Message.Contains("search", StringComparison.Ordinal));
            }

            // Disposing the returned scope triggers the "API call finished" log inside
            // the common library's ActivityScope — proving the IDisposable returned
            // from MethodInfo.Invoke is the real one, not NoopDisposable.
            Assert.Contains(logger.Entries, e =>
                e.Level == LogLevel.Information &&
                e.Message.Contains("API call finished", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    [Fact]
    public void CompleteApi_FlagOn_CommonReachable_InvokesReflectively()
    {
        EnsureCommonLoaded();
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", "1");
            CapturingLogger logger = new();

            ObservabilityShim.CompleteApi(logger, "tidal", "track", 200, true, TimeSpan.FromMilliseconds(42));

            Assert.Contains(logger.Entries, e =>
                e.Level == LogLevel.Information &&
                e.Message.Contains("API call completed", StringComparison.Ordinal) &&
                e.Message.Contains("tidal", StringComparison.Ordinal) &&
                e.Message.Contains("track", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    [Fact]
    public void Reflective_Path_Repeated_Invocations_AllSucceed()
    {
        // Each shim call re-resolves Type.GetType + GetMethod (no static caching);
        // repeated calls must remain stable and idempotent.
        EnsureCommonLoaded();
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", "1");
            CapturingLogger logger = new();

            for (int i = 0; i < 3; i++)
            {
                using (ObservabilityShim.StartApi(logger, "tidal", "ep" + i)) { }
                ObservabilityShim.CompleteApi(logger, "tidal", "ep" + i, 200, true, TimeSpan.FromMilliseconds(i));
            }

            // 3 starts + 3 finishes (from disposal) + 3 completes => 9 Information entries
            int infoCount = logger.Entries.Count(e => e.Level == LogLevel.Information);
            Assert.True(infoCount >= 9, $"Expected at least 9 Information entries, got {infoCount}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    [Fact]
    public void StartApi_FlagOn_CommonReachable_ScopeIsNotNoop()
    {
        EnsureCommonLoaded();
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", "1");
            CapturingLogger logger = new();

            using IDisposable scope = ObservabilityShim.StartApi(logger, "tidal", "albums");
            Assert.NotNull(scope);
            // The Noop disposable is a private nested type; assert by behavior:
            // a real ActivityScope writes a "finished" entry on dispose, the Noop does not.
            int beforeDispose = logger.Entries.Count;
            scope.Dispose();
            Assert.True(logger.Entries.Count > beforeDispose,
                "Disposing a real ActivityScope should emit a log entry");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
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
