// <copyright file="ProviderLifecycleTests.cs" company="RicherTunes">
// Copyright (c) RicherTunes. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Lidarr.Plugin.Common.Abstractions.Llm;
using Lidarr.Plugin.Common.Observability;
using Microsoft.Extensions.Logging;
using Tidalarr.Infrastructure.Logging;
using Xunit;

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
    private sealed class MockTidalProvider : ITidalProvider
    {
        private readonly ILogger _logger;
        private readonly bool _simulateFailure;
        private readonly int _resultCount;

        public MockTidalProvider(ILogger logger, bool simulateFailure = false, int resultCount = 1)
        {
            _logger = logger;
            _simulateFailure = simulateFailure;
            _resultCount = resultCount;
        }

        public string ProviderNameValue => ProviderName;

        public async Task<ProviderHealthResult> TestConnectionAsync()
        {
            var correlationId = Guid.NewGuid().ToString("N");

            // Log request start using Common library extension
            Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestStart(_logger, PluginName, ProviderName, "TestConnection", correlationId, "health_check", 1);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (_simulateFailure)
                {
                    stopwatch.Stop();
                    Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestError(
                        _logger,
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
                    _logger,
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
                    _logger,
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

        public Task<ProviderHealthResult> TestConnectionAsync(System.Threading.CancellationToken cancellationToken)
            => TestConnectionAsync();

        public Task<string> SearchAsync(string query, int count = 10)
        {
            var correlationId = Guid.NewGuid().ToString("N");

            // Use Common library extension explicitly
            Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestStart(_logger, PluginName, ProviderName, "Search", correlationId, "search", 1);

            try
            {
                if (_simulateFailure)
                {
                    Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestError(
                        _logger,
                        PluginName,
                        ProviderName,
                        "Search",
                        correlationId,
                        "API_ERROR",
                        "Search API failed",
                        new InvalidOperationException("API unavailable"));

                    return Task.FromResult<string>(string.Empty);
                }

                Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestComplete(
                    _logger,
                    PluginName,
                    ProviderName,
                    "Search",
                    correlationId,
                    150,
                    0,
                    _resultCount);

                return Task.FromResult($"{{\"results\":[{string.Join(",", Enumerable.Range(1, _resultCount))}]}}");
            }
            catch (Exception ex)
            {
                Lidarr.Plugin.Common.Observability.LlmLoggerExtensions.LogRequestError(
                    _logger,
                    PluginName,
                    ProviderName,
                    "Search",
                    correlationId,
                    "UNKNOWN_ERROR",
                    ex.Message,
                    ex);

                return Task.FromResult<string>(string.Empty);
            }
        }
    }

    [Fact]
    public async Task Provider_LogsStartEvent_WhenOperationBegins()
    {
        // Arrange
        var testLogger = new TestLogger();
        var provider = new MockTidalProvider(testLogger, simulateFailure: false);

        // Act
        await provider.TestConnectionAsync();

        // Assert
        var entries = testLogger.Entries;
        entries.Should().NotBeEmpty();
        entries.Should().Contain(e =>
            e.Message.Contains("Request started") &&
            e.Message.Contains(PluginName) &&
            e.Message.Contains(ProviderName) &&
            e.Message.Contains("TestConnection"));

        var startEntry = entries.FirstOrDefault(e =>
            e.Message.Contains("Request started") &&
            e.Message.Contains("TestConnection"));
        startEntry.Should().NotBeNull();
        // The Common library uses structured logging: "Request started: Tidalarr MockTidalProvider TestConnection {CorrelationId} Model=..."
        // The correlation ID appears as a GUID value in the message
        startEntry.Message.Should().Match("*Request started: Tidalarr MockTidalProvider TestConnection *");
    }

    [Fact]
    public async Task Provider_LogsCompleteEvent_WhenOperationSucceeds()
    {
        // Arrange
        var testLogger = new TestLogger();
        var provider = new MockTidalProvider(testLogger, simulateFailure: false);

        // Act
        await provider.TestConnectionAsync();

        // Assert
        var entries = testLogger.Entries;
        entries.Should().Contain(e =>
            e.Message.Contains("Request completed") &&
            e.Message.Contains(PluginName) &&
            e.Message.Contains(ProviderName) &&
            e.Message.Contains("TestConnection"));

        var completeEntry = entries.FirstOrDefault(e =>
            e.Message.Contains("Request completed") &&
            e.Message.Contains("TestConnection"));
        completeEntry.Should().NotBeNull();
        completeEntry.Message.Should().Contain("ElapsedMs=");
    }

    [Fact]
    public async Task Provider_LogsErrorEvent_WhenOperationFails()
    {
        // Arrange
        var testLogger = new TestLogger();
        var provider = new MockTidalProvider(testLogger, simulateFailure: true);

        // Act
        await provider.TestConnectionAsync();

        // Assert
        var entries = testLogger.Entries;
        entries.Should().Contain(e =>
            e.Message.Contains("Request error") &&
            e.Message.Contains(PluginName) &&
            e.Message.Contains(ProviderName) &&
            e.Message.Contains("TestConnection"));

        var errorEntry = entries.FirstOrDefault(e =>
            e.Message.Contains("Request error") &&
            e.Message.Contains("TestConnection"));
        errorEntry.Should().NotBeNull();
        errorEntry.Message.Should().Contain("ErrorCode=");
        errorEntry.Message.Should().Contain("AUTH_FAILED");
    }

    [Fact]
    public async Task Provider_LogsBothStartAndComplete_WhenOperationSucceeds()
    {
        // Arrange
        var testLogger = new TestLogger();
        var provider = new MockTidalProvider(testLogger, simulateFailure: false);

        // Act
        await provider.TestConnectionAsync();

        // Assert
        var entries = testLogger.Entries;
        var startEntry = entries.FirstOrDefault(e => e.Message.Contains("Request started"));
        var completeEntry = entries.FirstOrDefault(e => e.Message.Contains("Request completed"));

        startEntry.Should().NotBeNull();
        completeEntry.Should().NotBeNull();

        // Verify ordering: start should come before complete
        entries.IndexOf(startEntry).Should().BeLessThan(entries.IndexOf(completeEntry));
    }

    [Fact]
    public async Task Provider_LogsRequiredFields_WhenEventEmitted()
    {
        // Arrange
        var testLogger = new TestLogger();
        var provider = new MockTidalProvider(testLogger, simulateFailure: false);

        // Act
        await provider.TestConnectionAsync();

        // Assert
        var entries = testLogger.Entries;
        var startEntry = entries.FirstOrDefault(e => e.Message.Contains("Request started"));

        startEntry.Should().NotBeNull();
        startEntry.Message.Should().Contain(PluginName);
        startEntry.Message.Should().Contain(ProviderName);
        startEntry.Message.Should().Contain("TestConnection");
        // The Common library uses structured logging: correlation ID is embedded as GUID value
        // Verify the log format matches: "Request started: Tidalarr MockTidalProvider TestConnection {guid} Model=..."
        startEntry.Message.Should().Match("*Request started: Tidalarr MockTidalProvider TestConnection * Model=*");
    }

    [Fact]
    public async Task Provider_LogsCompleteWithCorrectItemCount_WhenMultipleResultsReturned()
    {
        // Arrange
        var testLogger = new TestLogger();
        var provider = new MockTidalProvider(testLogger, simulateFailure: false, resultCount: 5);

        // Act
        await provider.SearchAsync("test query", 10);

        // Assert
        var entries = testLogger.Entries;
        var completeEntry = entries.FirstOrDefault(e =>
            e.Message.Contains("Request completed") &&
            e.Message.Contains("Search"));

        completeEntry.Should().NotBeNull();
        completeEntry.Message.Should().Contain("OutputTokens=5");
    }

    [Fact]
    public async Task Provider_LogsErrorWithExceptionDetails_WhenOperationFails()
    {
        // Arrange
        var testLogger = new TestLogger();
        var provider = new MockTidalProvider(testLogger, simulateFailure: true);

        // Act
        await provider.TestConnectionAsync();

        // Assert
        var entries = testLogger.Entries;
        var errorEntry = entries.FirstOrDefault(e => e.Message.Contains("Request error"));

        errorEntry.Should().NotBeNull();
        errorEntry.Message.Should().Contain("ErrorCode=AUTH_FAILED");
        errorEntry.Message.Should().Contain("Error=");

        // Verify exception is captured (not null)
        var errorWithException = entries.FirstOrDefault(e =>
            e.Message.Contains("Request error") &&
            e.Exception != null);
        errorWithException.Should().NotBeNull();
    }

    /// <summary>
    /// Test logger that captures log entries for verification.
    /// Uses Microsoft.Extensions.Logging.ILogger to match Common library extensions.
    /// </summary>
    private class TestLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

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
        Task<ProviderHealthResult> TestConnectionAsync(System.Threading.CancellationToken cancellationToken = default);
        Task<string> SearchAsync(string query, int count = 10);
    }
}
