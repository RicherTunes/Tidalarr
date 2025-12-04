using Microsoft.Extensions.Logging;
using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Telemetry;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// 100% Coverage: TidalTelemetry observability testing
/// Tests all logging, metrics, and activity tracking
/// </summary>
public class TidalTelemetryTests
{
    private readonly MockLogger<TidalTelemetry> _mockLogger;
    private readonly TidalTelemetry _telemetry;

    public TidalTelemetryTests()
    {
        this._mockLogger = new MockLogger<TidalTelemetry>();
        this._telemetry = new TidalTelemetry(this._mockLogger);
    }

    [Fact]
    public void TidalTelemetry_Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => new TidalTelemetry(null!));
    }

    [Fact]
    public void TidalTelemetry_TrackDownloadStarted_LogsCorrectly()
    {
        // Act
        this._telemetry.TrackDownloadStarted("track123", TidalQuality.Lossless);

        // Assert
        MockLogEntry logEntry = Assert.Single(this._mockLogger.LogEntries);
        Assert.Equal(LogLevel.Information, logEntry.LogLevel);
        Assert.Contains("track123", logEntry.Message);
        Assert.Contains("Lossless", logEntry.Message);
        Assert.Contains("Download started", logEntry.Message);
    }

    [Fact]
    public void TidalTelemetry_TrackDownloadCompleted_LogsWithMetrics()
    {
        // Act
        this._telemetry.TrackDownloadCompleted("track456", TidalQuality.HiRes, TimeSpan.FromSeconds(30), 50_000_000);

        // Assert
        MockLogEntry logEntry = Assert.Single(this._mockLogger.LogEntries);
        Assert.Equal(LogLevel.Information, logEntry.LogLevel);
        Assert.Contains("track456", logEntry.Message);
        Assert.Contains("HiRes", logEntry.Message);
        Assert.Contains("30000", logEntry.Message); // Duration in milliseconds
        Assert.Contains("50000000", logEntry.Message); // File size
    }

    [Fact]
    public void TidalTelemetry_TrackDownloadFailed_LogsException()
    {
        // Arrange
        InvalidOperationException testException = new InvalidOperationException("Download failed");

        // Act
        this._telemetry.TrackDownloadFailed("track789", TidalQuality.High, testException);

        // Assert
        MockLogEntry logEntry = Assert.Single(this._mockLogger.LogEntries);
        Assert.Equal(LogLevel.Error, logEntry.LogLevel);
        Assert.Contains("track789", logEntry.Message);
        Assert.Contains("High", logEntry.Message);
        Assert.Same(testException, logEntry.Exception);
    }

    [Fact]
    public void TidalTelemetry_TrackApiCall_LogsWithLatency()
    {
        // Act
        this._telemetry.TrackApiCall("search", 200, TimeSpan.FromMilliseconds(500));

        // Assert
        MockLogEntry logEntry = Assert.Single(this._mockLogger.LogEntries);
        Assert.Equal(LogLevel.Debug, logEntry.LogLevel);
        Assert.Contains("search", logEntry.Message);
        Assert.Contains("200", logEntry.Message);
        Assert.Contains("500", logEntry.Message);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "Invalid credentials")]
    [InlineData(false, "")]
    public void TidalTelemetry_TrackAuthentication_LogsSuccessAndFailure(bool success, string? errorMessage)
    {
        // Act
        this._telemetry.TrackAuthentication(success, errorMessage);

        // Assert
        MockLogEntry logEntry = Assert.Single(this._mockLogger.LogEntries);

        if (success)
        {
            Assert.Equal(LogLevel.Information, logEntry.LogLevel);
            Assert.Contains("successful", logEntry.Message);
        }
        else
        {
            Assert.Equal(LogLevel.Warning, logEntry.LogLevel);
            Assert.Contains("failed", logEntry.Message);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Assert.Contains(errorMessage, logEntry.Message);
            }
        }
    }

    [Fact]
    public void TidalTelemetry_StartActivity_ReturnsDisposableActivity()
    {
        // Act
        IDisposable activity = this._telemetry.StartActivity("test-operation");

        // Assert
        Assert.NotNull(activity);
        _ = Assert.IsAssignableFrom<IDisposable>(activity);

        // Should not throw when disposed
        activity.Dispose();
    }

    [Fact]
    public void TidalTelemetry_CircuitBreakerEvents_LogCorrectly()
    {
        // Test circuit breaker opened
        TimeoutException testException = new TimeoutException("Service timeout");
        this._telemetry.CircuitBreakerOpened("TidalAPI", testException);

        _ = Assert.Single(this._mockLogger.LogEntries);
        MockLogEntry openedEntry = this._mockLogger.LogEntries.First();
        Assert.Equal(LogLevel.Warning, openedEntry.LogLevel);
        Assert.Contains("Circuit breaker OPENED", openedEntry.Message);

        // Clear and test circuit breaker closed
        this._mockLogger.LogEntries.Clear();
        this._telemetry.CircuitBreakerClosed("TidalAPI");

        _ = Assert.Single(this._mockLogger.LogEntries);
        MockLogEntry closedEntry = this._mockLogger.LogEntries.First();
        Assert.Equal(LogLevel.Information, closedEntry.LogLevel);
        Assert.Contains("Circuit breaker CLOSED", closedEntry.Message);
    }
}

public class MockLogger<T> : ILogger<T>
{
    public List<MockLogEntry> LogEntries { get; } = [];

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        private NoopDisposable() { }
        public void Dispose() { }
    }


    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        return NoopDisposable.Instance;
    }

    bool ILogger.IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        LogEntries.Add(new MockLogEntry
        {
            LogLevel = logLevel,
            EventId = eventId,
            Message = formatter(state, exception),
            Exception = exception
        });
    }
}

public class MockLogEntry
{
    public LogLevel LogLevel { get; set; }
    public EventId EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}




