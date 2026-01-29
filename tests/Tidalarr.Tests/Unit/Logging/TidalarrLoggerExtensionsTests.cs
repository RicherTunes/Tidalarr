using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Xunit;
using Tidalarr.Infrastructure.Logging;

namespace Tidalarr.Tests.Unit.Logging
{
    /// <summary>
    /// Log contract tests validating structured logging shape and redaction.
    /// </summary>
    public class TidalarrLoggerExtensionsTests
    {
        private readonly TestLogger _testLogger;

        public TidalarrLoggerExtensionsTests()
        {
            _testLogger = new TestLogger();
        }

        [Fact]
        public void LogRequestStart_IncludesRequiredFields()
        {
            // Arrange
            var correlationId = "abc12345";

            // Act
            _testLogger.LogRequestStart("TidalApi", "GetAlbum", correlationId, "album-123");

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(3000, entry.EventId.Id);
            Assert.Contains("[Tidalarr]", entry.Message);
            Assert.Contains("TidalApi", entry.Message);
            Assert.Contains("GetAlbum", entry.Message);
            Assert.Contains("CorrelationId=abc12345", entry.Message);
            Assert.Contains("started", entry.Message);
        }

        [Fact]
        public void LogRequestComplete_IncludesElapsedMs()
        {
            // Arrange
            var correlationId = "def67890";

            // Act
            _testLogger.LogRequestComplete("TidalApi", "Search", correlationId, 1234, 10);

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(3001, entry.EventId.Id);
            Assert.Contains("ElapsedMs=1234", entry.Message);
            Assert.Contains("ItemCount=10", entry.Message);
            Assert.Contains("completed", entry.Message);
        }

        [Fact]
        public void LogRequestError_UsesErrorLogLevel()
        {
            // Arrange
            var correlationId = "err12345";

            // Act
            _testLogger.LogRequestError("TidalApi", "GetTrack", correlationId, "HTTP_404", "Not found");

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal(3002, entry.EventId.Id);
            Assert.Contains("ErrorCode=HTTP_404", entry.Message);
        }

        [Fact]
        public void LogAuthSuccess_UsesCorrectEventId()
        {
            // Arrange
            var correlationId = "auth1234";

            // Act
            _testLogger.LogAuthSuccess(correlationId);

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(3010, entry.EventId.Id);
            Assert.Contains("Authentication succeeded", entry.Message);
        }

        [Fact]
        public void LogAuthFail_RedactsSensitiveData()
        {
            // Arrange
            var correlationId = "fail1234";
            var sensitiveReason = "Invalid token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4iLCJpYXQiOjE1MTYyMzkwMjJ9.abc123";

            // Act
            _testLogger.LogAuthFail(correlationId, sensitiveReason);

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal(3011, entry.EventId.Id);
            Assert.Contains("[REDACTED]", entry.Message);
            Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", entry.Message);
        }

        [Fact]
        public void LogTokenRefreshSuccess_IncludesExpiry()
        {
            // Arrange
            var correlationId = "ref12345";
            var expiresIn = TimeSpan.FromMinutes(60);

            // Act
            _testLogger.LogTokenRefreshSuccess(correlationId, expiresIn);

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(3012, entry.EventId.Id);
            Assert.Contains("ExpiresInMinutes=60", entry.Message);
        }

        [Fact]
        public void LogRateLimited_IncludesRetryAfter()
        {
            // Arrange
            var correlationId = "rate1234";
            var retryAfter = TimeSpan.FromSeconds(30);

            // Act
            _testLogger.LogRateLimited("TidalApi", correlationId, retryAfter);

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal(3020, entry.EventId.Id);
            Assert.Contains("rate limited", entry.Message);
            Assert.Contains("RetryAfterMs=30000", entry.Message);
        }

        [Fact]
        public void LogDownloadStart_IncludesAlbumInfo()
        {
            // Arrange
            var correlationId = "dl123456";

            // Act
            _testLogger.LogDownloadStart(correlationId, "album-xyz", 12, "LOSSLESS");

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(3030, entry.EventId.Id);
            Assert.Contains("AlbumId=album-xyz", entry.Message);
            Assert.Contains("TrackCount=12", entry.Message);
            Assert.Contains("Quality=LOSSLESS", entry.Message);
        }

        [Fact]
        public void LogApiCallComplete_UsesWarningForErrors()
        {
            // Arrange
            var correlationId = "api12345";

            // Act - success (200)
            _testLogger.LogApiCallComplete("/albums/123", correlationId, 200, 100);

            // Act - error (404)
            _testLogger.LogApiCallComplete("/albums/999", correlationId, 404, 50);

            // Assert
            Assert.Equal(2, _testLogger.Entries.Count);
            Assert.Equal(LogLevel.Debug, _testLogger.Entries[0].Level); // 200 = Debug
            Assert.Equal(LogLevel.Warning, _testLogger.Entries[1].Level); // 404 = Warning
        }

        [Fact]
        public void LogSearch_TruncatesLongQueries()
        {
            // Arrange
            var correlationId = "srch1234";
            var longQuery = "This is a very long search query that exceeds the maximum allowed length for logging";

            // Act
            _testLogger.LogSearch(correlationId, longQuery, 42, 123);

            // Assert
            var entry = _testLogger.Entries.Single();
            Assert.Equal(3060, entry.EventId.Id);
            Assert.Contains("...", entry.Message);
            Assert.DoesNotContain("for logging", entry.Message);
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
                    Message = formatter(state, exception),
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
    }
}
