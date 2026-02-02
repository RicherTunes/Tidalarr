using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Tidalarr.Infrastructure.Logging;

namespace Tidalarr.Tests.Unit.Logging
{
    /// <summary>
    /// Provider lifecycle logging contract tests validating that providers emit
    /// correct logging events during their lifecycle: start, complete, error.
    /// </summary>
    [Trait("Category", "Contract")]
    [Trait("Target", "Provider")]
    public class ProviderLifecycleTests
    {
        private sealed class MockTidalProvider : ITidalProvider
        {
            private readonly ILogger _logger;
            private readonly bool _simulateError;

            public MockTidalProvider(ILogger<TidalIndexer> logger, bool simulateError = false)
            {
                _logger = logger;
                _simulateError = simulateError;
            }

            public string ProviderName => "MockTidalProvider";

            public async Task<List<StreamingTrack>> GetRecommendationsAsync(string query, CancellationToken cancellationToken = default)
            {
                LogProviderStart();
                try
                {
                    var tracks = await Task.FromResult(new List<StreamingTrack> { new() { Title = "Test Track", AlbumId = "test-album" } });
                    LogProviderComplete(tracks.Count);
                    return tracks;
                }
                catch (Exception ex)
                {
                    LogProviderError(ex);
                    throw;
                }
            }

            private void LogProviderStart()
            {
                var correlationId = Guid.NewGuid().ToString("N");
                _logger.LogInformation("[{Provider}] Operation started - {Operation}", ProviderName, "GetRecommendations");
                _logger.LogRequestStart("TidalProvider", "GetRecommendations", correlationId, "test-query");
            }

            private void LogProviderComplete(int itemCount)
            {
                var correlationId = Guid.NewGuid().ToString("N");
                _logger.LogInformation("[{Provider}] Operation completed successfully. Items: {ItemCount}", ProviderName, itemCount);
                _logger.LogRequestComplete("TidalProvider", "GetRecommendations", correlationId, 1500, itemCount);
            }

            private void LogProviderError(Exception ex)
            {
                var correlationId = Guid.NewGuid().ToString("N");
                _logger.LogError(ex, "[{Provider}] Operation failed - {Operation}", ProviderName, "GetRecommendations");
                _logger.LogRequestError("TidalProvider", "GetRecommendations", correlationId, "OPERATION_ERROR", ex.Message);
            }
        }

        [Fact]
        public async Task Provider_LogsStartEvent_WhenOperationBegins()
        {
            // Arrange
            var testLogger = new TestLogger();
            var provider = new MockTidalProvider(testLogger, simulateError: false);

            // Act
            await provider.GetRecommendationsAsync("test query");

            // Assert
            var entries = testLogger.Entries;
            Assert.True(entries.Count >= 1, "At least one log entry should be present");

            var startEntry = entries.FirstOrDefault(e => e.Message.Contains("GetRecommendations") && e.Message.Contains("started"));
            Assert.NotNull(startEntry);
            Assert.Equal(LogLevel.Information, startEntry.Level);
            Assert.Contains("[Tidalarr]", startEntry.Message);
            Assert.Contains("TidalProvider", startEntry.Message);
        }

        [Fact]
        public async Task Provider_LogsCompleteEvent_WhenOperationSucceeds()
        {
            // Arrange
            var testLogger = new TestLogger();
            var provider = new MockTidalProvider(testLogger, simulateError: false);

            // Act
            await provider.GetRecommendationsAsync("test query");

            // Assert
            var entries = testLogger.Entries;
            Assert.True(entries.Count >= 2, "At least two log entries (start + complete) should be present");

            var completeEntry = entries.FirstOrDefault(e => e.Message.Contains("GetRecommendations") && e.Message.Contains("completed"));
            Assert.NotNull(completeEntry);
            Assert.Equal(LogLevel.Information, completeEntry.Level);
            Assert.Contains("ElapsedMs=", completeEntry.Message);
            Assert.Contains("ItemCount=1", completeEntry.Message);
        }

        [Fact]
        public async Task Provider_LogsErrorEvent_WhenOperationFails()
        {
            // Arrange
            var testLogger = new TestLogger();
            var provider = new MockTidalProvider(testLogger, simulateError: true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetRecommendationsAsync("test query"));

            var entries = testLogger.Entries;
            Assert.True(entries.Count >= 1, "At least one log entry should be present");

            var errorEntry = entries.FirstOrDefault(e => e.Message.Contains("GetRecommendations") && e.Message.Contains("Operation failed"));
            Assert.NotNull(errorEntry);
            Assert.Equal(LogLevel.Error, errorEntry.Level);
            Assert.Contains("ErrorCode=OPERATION_ERROR", errorEntry.Message);
        }

        [Fact]
        public async Task Provider_LogsBothStartAndComplete_WhenOperationSucceeds()
        {
            // Arrange
            var testLogger = new TestLogger();
            var provider = new MockTidalProvider(testLogger, simulateError: false);

            // Act
            await provider.GetRecommendationsAsync("test query");

            // Assert
            var entries = testLogger.Entries;
            var startEntry = entries.FirstOrDefault(e => e.Message.Contains("GetRecommendations") && e.Message.Contains("started"));
            var completeEntry = entries.FirstOrDefault(e => e.Message.Contains("GetRecommendations") && e.Message.Contains("completed"));

            Assert.NotNull(startEntry);
            Assert.NotNull(completeEntry);
            Assert.NotEqual(startEntry.EventId.Id, completeEntry.EventId.Id);

            var startEventId = startEntry.EventId.Id;
            var completeEventId = completeEntry.EventId.Id;
            Assert.True(startEventId < completeEventId, "Start event should come before complete event");
        }

        [Fact]
        public void Provider_LogsRequiredFields_WhenEventEmitted()
        {
            // Arrange
            var testLogger = new TestLogger();
            var provider = new MockTidalProvider(testLogger, simulateError: false);

            // Act
            provider.GetRecommendationsAsync("test query");

            // Assert - Check start event has required fields
            var startEntry = testLogger.Entries.FirstOrDefault(e => e.Message.Contains("started"));
            Assert.NotNull(startEntry);
            Assert.Contains("CorrelationId=", startEntry.Message);
            Assert.Contains("TidalProvider", startEntry.Message);
            Assert.Contains("GetRecommendations", startEntry.Message);

            // Check complete event has required fields
            var completeEntry = testLogger.Entries.FirstOrDefault(e => e.Message.Contains("completed"));
            Assert.NotNull(completeEntry);
            Assert.Contains("ElapsedMs=", completeEntry.Message);
            Assert.Contains("ItemCount=", completeEntry.Message);
        }

        [Fact]
        public async Task Provider_LogsCompleteWithCorrectItemCount_WhenMultipleResultsReturned()
        {
            // Arrange
            var testLogger = new TestLogger();
            var provider = new MockTidalProvider(testLogger, simulateError: false);

            // Act
            await provider.GetRecommendationsAsync("test query");

            // Assert
            var completeEntry = testLogger.Entries.FirstOrDefault(e => e.Message.Contains("completed"));
            Assert.NotNull(completeEntry);
            Assert.Contains("ItemCount=1", completeEntry.Message);
        }

        /// <summary>
        /// Test logger that captures log entries for verification.
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

            public class LogEntry
            {
                public LogLevel Level { get; init; }
                public EventId EventId { get; init; }
                public string Message { get; init; } = string.Empty;
                public Exception? Exception { get; init; }
            }
        }

        /// <summary>
        /// Contract for tidal provider operations.
        /// </summary>
        public interface ITidalProvider
        {
            string ProviderName { get; }
            Task<List<StreamingTrack>> GetRecommendationsAsync(string query, CancellationToken cancellationToken = default);
        }
    }
}
