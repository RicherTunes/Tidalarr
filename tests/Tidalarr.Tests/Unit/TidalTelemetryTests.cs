using Microsoft.Extensions.Logging;
using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Telemetry;
using Xunit;

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
        _mockLogger = new MockLogger<TidalTelemetry>();
        _telemetry = new TidalTelemetry(_mockLogger);
    }
    
    [Fact]
    public void TidalTelemetry_Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TidalTelemetry(null!));
    }
    
    [Fact]
    public void TidalTelemetry_TrackDownloadStarted_LogsCorrectly()
    {
        // Act
        _telemetry.TrackDownloadStarted("track123", TidalQuality.Lossless);
        
        // Assert
        var logEntry = Assert.Single(_mockLogger.LogEntries);
        Assert.Equal(LogLevel.Information, logEntry.LogLevel);
        Assert.Contains("track123", logEntry.Message);
        Assert.Contains("Lossless", logEntry.Message);
        Assert.Contains("Download started", logEntry.Message);
    }
    
    [Fact]
    public void TidalTelemetry_TrackDownloadCompleted_LogsWithMetrics()
    {
        // Act
        _telemetry.TrackDownloadCompleted("track456", TidalQuality.HiRes, TimeSpan.FromSeconds(30), 50_000_000);
        
        // Assert
        var logEntry = Assert.Single(_mockLogger.LogEntries);
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
        var testException = new InvalidOperationException("Download failed");
        
        // Act
        _telemetry.TrackDownloadFailed("track789", TidalQuality.High, testException);
        
        // Assert
        var logEntry = Assert.Single(_mockLogger.LogEntries);
        Assert.Equal(LogLevel.Error, logEntry.LogLevel);
        Assert.Contains("track789", logEntry.Message);
        Assert.Contains("High", logEntry.Message);
        Assert.Same(testException, logEntry.Exception);
    }
    
    [Fact]
    public void TidalTelemetry_TrackApiCall_LogsWithLatency()
    {
        // Act
        _telemetry.TrackApiCall("search", 200, TimeSpan.FromMilliseconds(500));
        
        // Assert
        var logEntry = Assert.Single(_mockLogger.LogEntries);
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
        _telemetry.TrackAuthentication(success, errorMessage);
        
        // Assert
        var logEntry = Assert.Single(_mockLogger.LogEntries);
        
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
        var activity = _telemetry.StartActivity("test-operation");
        
        // Assert
        Assert.NotNull(activity);
        Assert.IsAssignableFrom<IDisposable>(activity);
        
        // Should not throw when disposed
        activity.Dispose();
    }
    
    [Fact]
    public void TidalTelemetry_CircuitBreakerEvents_LogCorrectly()
    {
        // Test circuit breaker opened
        var testException = new TimeoutException("Service timeout");
        _telemetry.CircuitBreakerOpened("TidalAPI", testException);
        
        Assert.Single(_mockLogger.LogEntries);
        var openedEntry = _mockLogger.LogEntries.First();
        Assert.Equal(LogLevel.Warning, openedEntry.LogLevel);
        Assert.Contains("Circuit breaker OPENED", openedEntry.Message);
        
        // Clear and test circuit breaker closed
        _mockLogger.LogEntries.Clear();
        _telemetry.CircuitBreakerClosed("TidalAPI");
        
        Assert.Single(_mockLogger.LogEntries);
        var closedEntry = _mockLogger.LogEntries.First();
        Assert.Equal(LogLevel.Information, closedEntry.LogLevel);
        Assert.Contains("Circuit breaker CLOSED", closedEntry.Message);
    }
}

public class MockLogger<T> : ILogger<T>
{
    public List<MockLogEntry> LogEntries { get; } = new();
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
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