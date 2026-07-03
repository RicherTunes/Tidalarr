using Tidalarr.Core.Exceptions;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// 100% Coverage: Custom exception hierarchy testing
/// Tests all exception types, constructors, properties, and inheritance
/// </summary>
public class TidalExceptionsTests
{
    [Fact]
    public void TidalException_Constructor_WithMessage_SetsMessageCorrectly()
    {
        // Arrange & Act
        TidalException exception = new("Test message");

        // Assert
        Assert.Equal("Test message", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void TidalException_Constructor_WithMessageAndInnerException_SetsBoth()
    {
        // Arrange
        ArgumentException innerException = new("Inner error");

        // Act
        TidalException exception = new("Outer message", innerException);

        // Assert
        Assert.Equal("Outer message", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void TidalAuthenticationException_InheritsFrom_TidalException()
    {
        // Arrange & Act
        TidalAuthenticationException exception = new("Auth failed");

        // Assert
        _ = Assert.IsAssignableFrom<TidalException>(exception);
        Assert.Equal("Auth failed", exception.Message);
    }

    [Fact]
    public void TidalAuthenticationException_Constructor_WithInnerException_SetsCorrectly()
    {
        // Arrange
        HttpRequestException innerException = new("HTTP error");

        // Act
        TidalAuthenticationException exception = new("Authentication failed", innerException);

        // Assert
        Assert.Equal("Authentication failed", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void TidalRateLimitException_Constructor_SetsRetryAfterSeconds()
    {
        // Arrange & Act
        TidalRateLimitException exception = new(120, "Rate limited");

        // Assert
        Assert.Equal(120, exception.RetryAfterSeconds);
        Assert.Equal("Rate limited", exception.Message);
        _ = Assert.IsAssignableFrom<TidalException>(exception);
    }

    [Fact]
    public void TidalStreamUnavailableException_Constructor_SetsTrackIdAndQuality()
    {
        // Arrange & Act
        TidalStreamUnavailableException exception = new(
            "track123",
            TidalQuality.HiRes,
            "Stream not available");

        // Assert
        Assert.Equal("track123", exception.TrackId);
        Assert.Equal(TidalQuality.HiRes, exception.RequestedQuality);
        Assert.Equal("Stream not available", exception.Message);
        _ = Assert.IsAssignableFrom<TidalException>(exception);
    }

    [Fact]
    public void TidalStreamUnavailableException_DefaultReason_IsUnknownTransient()
    {
        // The legacy 3-arg ctor must keep working and default to the safe (non-permanent) reason so a
        // caller that has not classified the failure can never accidentally trigger suppression.
        TidalStreamUnavailableException exception = new("track123", TidalQuality.HiRes, "Stream not available");

        Assert.Equal(TidalStreamUnavailableReason.Unknown, exception.Reason);
        Assert.False(exception.Reason.IsPermanent());
    }

    [Fact]
    public void TidalStreamUnavailableException_CarriesClassifiedReason()
    {
        TidalStreamUnavailableException exception = new(
            "track123", TidalQuality.HiRes, "Rights removed", TidalStreamUnavailableReason.RightsRemoved);

        Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, exception.Reason);
        Assert.True(exception.Reason.IsPermanent());
    }

    [Fact]
    public void TidalApiException_Constructor_WithStatusCode_SetsStatusCode()
    {
        // Arrange & Act
        TidalApiException exception = new("API error", 404);

        // Assert
        Assert.Equal("API error", exception.Message);
        Assert.Equal(404, exception.StatusCode);
        _ = Assert.IsAssignableFrom<TidalException>(exception);
    }

    [Fact]
    public void TidalApiException_Constructor_WithInnerExceptionAndStatusCode_SetsBoth()
    {
        // Arrange
        HttpRequestException innerException = new("Network error");

        // Act
        TidalApiException exception = new("API failed", innerException, 500);

        // Assert
        Assert.Equal("API failed", exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(500, exception.StatusCode);
    }

    [Fact]
    public void TidalApiException_Constructor_WithoutStatusCode_StatusCodeIsNull()
    {
        // Arrange & Act
        TidalApiException exception = new("No status code");

        // Assert
        Assert.Null(exception.StatusCode);
    }

    [Fact]
    public void TidalManifestException_Constructor_SetsManifestType()
    {
        // Arrange & Act
        TidalManifestException exception = new("application/dash+xml", "Manifest parsing failed");

        // Assert
        Assert.Equal("application/dash+xml", exception.ManifestType);
        Assert.Equal("Manifest parsing failed", exception.Message);
        _ = Assert.IsAssignableFrom<TidalException>(exception);
    }

    [Theory]
    [InlineData(typeof(TidalException))]
    [InlineData(typeof(TidalAuthenticationException))]
    [InlineData(typeof(TidalRateLimitException))]
    [InlineData(typeof(TidalStreamUnavailableException))]
    [InlineData(typeof(TidalApiException))]
    [InlineData(typeof(TidalManifestException))]
    public void AllTidalExceptions_InheritFrom_Exception(Type exceptionType)
    {
        // Verify all our custom exceptions inherit from Exception
        Assert.True(typeof(Exception).IsAssignableFrom(exceptionType));
    }

    [Theory]
    [InlineData(typeof(TidalAuthenticationException))]
    [InlineData(typeof(TidalRateLimitException))]
    [InlineData(typeof(TidalStreamUnavailableException))]
    [InlineData(typeof(TidalApiException))]
    [InlineData(typeof(TidalManifestException))]
    public void AllSpecificExceptions_InheritFrom_TidalException(Type exceptionType)
    {
        // Verify all specific exceptions inherit from TidalException
        Assert.True(typeof(TidalException).IsAssignableFrom(exceptionType));
    }
}



