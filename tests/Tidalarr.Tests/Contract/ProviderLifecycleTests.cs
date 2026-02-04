// <copyright file="ProviderLifecycleTests.cs" company="RicherTunes">
// Copyright (c) RicherTunes. All rights reserved.
// </copyright>

using FluentAssertions;
using Lidarr.Plugin.Common.Abstractions.Llm;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Tests.Contract;

/// <summary>
/// Provider lifecycle logging contract tests validating that Tidalarr providers emit
/// correct request lifecycle events using Common library LlmLoggerExtensions.
/// Tests verify LogRequestStart, LogRequestComplete, and LogRequestError events.
/// </summary>
[Trait("Area", "Contract")]
[Trait("Target", "Provider")]
public class ProviderLifecycleTests
{
    private const string PluginName = "Tidalarr";
    private const string ProviderName = "MockTidalProvider";

    /// <summary>
    /// Mock Tidal provider for testing lifecycle logging.
    /// </summary>
    private sealed class MockTidalProvider(ILogger logger, bool simulateFailure = false, int resultCount = 1) : ITidalProvider
    {
        private readonly ILogger _logger = logger;
        private readonly bool _simulateFailure = simulateFailure;
        private readonly int _resultCount = resultCount;

        public string ProviderNameValue => ProviderName;

        public async Task<ProviderHealthResult> TestConnectionAsync()
        {
            string correlationId = Guid.NewGuid().ToString("N");

            // Log request start using Common library extension
            Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestStart(this._logger, PluginName, ProviderName, "TestConnection", correlationId, "health_check", 1);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (this._simulateFailure)
                {
                    stopwatch.Stop();
                    Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestError(
                        this._logger,
                        PluginName,
                        ProviderName,
                        "TestConnection",
                        correlationId,
                        "AUTH_FAILED",
                        "Authentication failed",
                        new InvalidOperationException("Simulated failure"));

                    return ProviderHealthResult.Unhealthy("Authentication failed");
                }

                await Task.Delay(10); // Simulate work
                stopwatch.Stop();

                // Log request complete using Common library extension
                Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestComplete(
                    this._logger,
                    PluginName,
                    ProviderName,
                    "TestConnection",
                    correlationId,
                    stopwatch.ElapsedMilliseconds,
                    0,
                    0);

                return ProviderHealthResult.Healthy(stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestError(
                    this._logger,
                    PluginName,
                    ProviderName,
                    "TestConnection",
                    correlationId,
                    "UNKNOWN_ERROR",
                    ex.Message,
                    ex);

                return ProviderHealthResult.Unhealthy(ex.Message);
            }
        }

        public Task<ProviderHealthResult> TestConnectionAsync(CancellationToken cancellationToken)
        {
            return TestConnectionAsync();
        }

        public Task<string> SearchAsync(string query, int count = 10)
        {
            string correlationId = Guid.NewGuid().ToString("N");

            // Use Common library extension explicitly
            Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestStart(this._logger, PluginName, ProviderName, "Search", correlationId, "search", 1);

            try
            {
                if (this._simulateFailure)
                {
                    Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestError(
                        this._logger,
                        PluginName,
                        ProviderName,
                        "Search",
                        correlationId,
                        "API_ERROR",
                        "Search API failed",
                        new InvalidOperationException("API unavailable"));

                    return Task.FromResult(string.Empty);
                }

                Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestComplete(
                    this._logger,
                    PluginName,
                    ProviderName,
                    "Search",
                    correlationId,
                    150,
                    0,
                    this._resultCount);

                return Task.FromResult($"{{\"results\":[{string.Join(",", Enumerable.Range(1, this._resultCount))}]}}");
            }
            catch (Exception ex)
            {
                Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestError(
                    this._logger,
                    PluginName,
                    ProviderName,
                    "Search",
                    correlationId,
                    "UNKNOWN_ERROR",
                    ex.Message,
                    ex);

                return Task.FromResult(string.Empty);
            }
        }
    }

    [Fact]
    public async Task Provider_LogsStartEvent_WhenOperationBegins()
    {
        // Arrange
        TestLogger testLogger = new();
        MockTidalProvider provider = new(testLogger, simulateFailure: false);

        // Act
        _ = await provider.TestConnectionAsync();

        // Assert
        List<TestLogger.LogEntry> entries = testLogger.Entries;
        _ = entries.Should().NotBeEmpty();
        _ = entries.Should().Contain(e =>
            e.Message.Contains("Request started") &&
            e.Message.Contains(PluginName) &&
            e.Message.Contains(ProviderName) &&
            e.Message.Contains("TestConnection"));

        TestLogger.LogEntry? startEntry = entries.FirstOrDefault(e =>
            e.Message.Contains("Request started") &&
            e.Message.Contains("TestConnection"));
        _ = startEntry.Should().NotBeNull();
        // The Common library uses structured logging: "Request started: Tidalarr MockTidalProvider TestConnection {CorrelationId} Model=..."
        // The correlation ID appears as a GUID value in the message
        _ = startEntry.Message.Should().Match("*Request started: Tidalarr MockTidalProvider TestConnection *");
    }

    [Fact]
    public async Task Provider_LogsCompleteEvent_WhenOperationSucceeds()
    {
        // Arrange
        TestLogger testLogger = new();
        MockTidalProvider provider = new(testLogger, simulateFailure: false);

        // Act
        _ = await provider.TestConnectionAsync();

        // Assert
        List<TestLogger.LogEntry> entries = testLogger.Entries;
        _ = entries.Should().Contain(e =>
            e.Message.Contains("Request completed") &&
            e.Message.Contains(PluginName) &&
            e.Message.Contains(ProviderName) &&
            e.Message.Contains("TestConnection"));

        TestLogger.LogEntry? completeEntry = entries.FirstOrDefault(e =>
            e.Message.Contains("Request completed") &&
            e.Message.Contains("TestConnection"));
        _ = completeEntry.Should().NotBeNull();
        _ = completeEntry.Message.Should().Contain("ElapsedMs=");
    }

    [Fact]
    public async Task Provider_LogsErrorEvent_WhenOperationFails()
    {
        // Arrange
        TestLogger testLogger = new();
        MockTidalProvider provider = new(testLogger, simulateFailure: true);

        // Act
        _ = await provider.TestConnectionAsync();

        // Assert
        List<TestLogger.LogEntry> entries = testLogger.Entries;
        _ = entries.Should().Contain(e =>
            e.Message.Contains("Request error") &&
            e.Message.Contains(PluginName) &&
            e.Message.Contains(ProviderName) &&
            e.Message.Contains("TestConnection"));

        TestLogger.LogEntry? errorEntry = entries.FirstOrDefault(e =>
            e.Message.Contains("Request error") &&
            e.Message.Contains("TestConnection"));
        _ = errorEntry.Should().NotBeNull();
        _ = errorEntry.Message.Should().Contain("ErrorCode=");
        _ = errorEntry.Message.Should().Contain("AUTH_FAILED");
    }

    [Fact]
    public async Task Provider_LogsBothStartAndComplete_WhenOperationSucceeds()
    {
        // Arrange
        TestLogger testLogger = new();
        MockTidalProvider provider = new(testLogger, simulateFailure: false);

        // Act
        _ = await provider.TestConnectionAsync();

        // Assert
        List<TestLogger.LogEntry> entries = testLogger.Entries;
        TestLogger.LogEntry? startEntry = entries.FirstOrDefault(e => e.Message.Contains("Request started"));
        TestLogger.LogEntry? completeEntry = entries.FirstOrDefault(e => e.Message.Contains("Request completed"));

        _ = startEntry.Should().NotBeNull();
        _ = completeEntry.Should().NotBeNull();

        // Verify ordering: start should come before complete
        _ = entries.IndexOf(startEntry).Should().BeLessThan(entries.IndexOf(completeEntry));
    }

    [Fact]
    public async Task Provider_LogsRequiredFields_WhenEventEmitted()
    {
        // Arrange
        TestLogger testLogger = new();
        MockTidalProvider provider = new(testLogger, simulateFailure: false);

        // Act
        _ = await provider.TestConnectionAsync();

        // Assert
        List<TestLogger.LogEntry> entries = testLogger.Entries;
        TestLogger.LogEntry? startEntry = entries.FirstOrDefault(e => e.Message.Contains("Request started"));

        _ = startEntry.Should().NotBeNull();
        _ = startEntry.Message.Should().Contain(PluginName);
        _ = startEntry.Message.Should().Contain(ProviderName);
        _ = startEntry.Message.Should().Contain("TestConnection");
        // The Common library uses structured logging: correlation ID is embedded as GUID value
        // Verify the log format matches: "Request started: Tidalarr MockTidalProvider TestConnection {guid} Model=..."
        _ = startEntry.Message.Should().Match("*Request started: Tidalarr MockTidalProvider TestConnection * Model=*");
    }

    [Fact]
    public async Task Provider_LogsCompleteWithCorrectItemCount_WhenMultipleResultsReturned()
    {
        // Arrange
        TestLogger testLogger = new();
        MockTidalProvider provider = new(testLogger, simulateFailure: false, resultCount: 5);

        // Act
        _ = await provider.SearchAsync("test query", 10);

        // Assert
        List<TestLogger.LogEntry> entries = testLogger.Entries;
        TestLogger.LogEntry? completeEntry = entries.FirstOrDefault(e =>
            e.Message.Contains("Request completed") &&
            e.Message.Contains("Search"));

        _ = completeEntry.Should().NotBeNull();
        _ = completeEntry.Message.Should().Contain("OutputTokens=5");
    }

    [Fact]
    public async Task Provider_LogsErrorWithExceptionDetails_WhenOperationFails()
    {
        // Arrange
        TestLogger testLogger = new();
        MockTidalProvider provider = new(testLogger, simulateFailure: true);

        // Act
        _ = await provider.TestConnectionAsync();

        // Assert
        List<TestLogger.LogEntry> entries = testLogger.Entries;
        TestLogger.LogEntry? errorEntry = entries.FirstOrDefault(e => e.Message.Contains("Request error"));

        _ = errorEntry.Should().NotBeNull();
        _ = errorEntry.Message.Should().Contain("ErrorCode=AUTH_FAILED");
        _ = errorEntry.Message.Should().Contain("Error=");

        // Verify exception is captured (not null)
        TestLogger.LogEntry? errorWithException = entries.FirstOrDefault(e =>
            e.Message.Contains("Request error") &&
            e.Exception != null);
        _ = errorWithException.Should().NotBeNull();
    }

    /// <summary>
    /// Test logger that captures log entries for verification.
    /// Uses Microsoft.Extensions.Logging.ILogger to match Common library extensions.
    /// </summary>
    private class TestLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry
            {
                Level = logLevel,
                EventId = eventId,
                Message = formatter(state, exception) ?? string.Empty,
                Exception = exception
            });
        }

        public void ClearEntries()
        {
            Entries.Clear();
        }

        public class LogEntry
        {
            public LogLevel Level { get; init; }
            public EventId EventId { get; init; }
            public string Message { get; init; } = string.Empty;
            public Exception? Exception { get; init; }
        }
    }

    /// <summary>
    /// Contract for Tidal provider operations.
    /// </summary>
    public interface ITidalProvider
    {
        string ProviderNameValue { get; }
        Task<ProviderHealthResult> TestConnectionAsync(CancellationToken cancellationToken = default);
        Task<string> SearchAsync(string query, int count = 10);
    }
}
