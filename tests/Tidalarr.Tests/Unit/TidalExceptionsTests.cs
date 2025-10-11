using Tidalarr.Core.Exceptions;
using Tidalarr.Core.Models;
using Xunit;

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
        var exception = new TidalException("Test message");

        // Assert
        Assert.Equal("Test message", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void TidalException_Constructor_WithMessageAndInnerException_SetsBoth()
    {
        // Arrange
        var innerException = new ArgumentException("Inner error");

        // Act
        var exception = new TidalException("Outer message", innerException);

        // Assert
        Assert.Equal("Outer message", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void TidalAuthenticationException_InheritsFrom_TidalException()
    {
        // Arrange & Act
        var exception = new TidalAuthenticationException("Auth failed");

        // Assert
        Assert.IsAssignableFrom<TidalException>(exception);
        Assert.Equal("Auth failed", exception.Message);
    }

    [Fact]
    public void TidalAuthenticationException_Constructor_WithInnerException_SetsCorrectly()
    {
        // Arrange
        var innerException = new HttpRequestException("HTTP error");

        // Act
        var exception = new TidalAuthenticationException("Authentication failed", innerException);

        // Assert
        Assert.Equal("Authentication failed", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void TidalRateLimitException_Constructor_SetsRetryAfterSeconds()
    {
        // Arrange & Act
        var exception = new TidalRateLimitException(120, "Rate limited");

        // Assert
        Assert.Equal(120, exception.RetryAfterSeconds);
        Assert.Equal("Rate limited", exception.Message);
        Assert.IsAssignableFrom<TidalException>(exception);
    }

    [Fact]
    public void TidalStreamUnavailableException_Constructor_SetsTrackIdAndQuality()
    {
        // Arrange & Act
        var exception = new TidalStreamUnavailableException(
            "track123",
            TidalQuality.HiRes,
            "Stream not available");

        // Assert
        Assert.Equal("track123", exception.TrackId);
        Assert.Equal(TidalQuality.HiRes, exception.RequestedQuality);
        Assert.Equal("Stream not available", exception.Message);
        Assert.IsAssignableFrom<TidalException>(exception);
    }

    [Fact]
    public void TidalApiException_Constructor_WithStatusCode_SetsStatusCode()
    {
        // Arrange & Act
        var exception = new TidalApiException("API error", 404);

        // Assert
        Assert.Equal("API error", exception.Message);
        Assert.Equal(404, exception.StatusCode);
        Assert.IsAssignableFrom<TidalException>(exception);
    }

    [Fact]
    public void TidalApiException_Constructor_WithInnerExceptionAndStatusCode_SetsBoth()
    {
        // Arrange
        var innerException = new HttpRequestException("Network error");

        // Act
        var exception = new TidalApiException("API failed", innerException, 500);

        // Assert
        Assert.Equal("API failed", exception.Message);
        Assert.Same(innerException, exception.InnerException);
        Assert.Equal(500, exception.StatusCode);
    }

    [Fact]
    public void TidalApiException_Constructor_WithoutStatusCode_StatusCodeIsNull()
    {
        // Arrange & Act
        var exception = new TidalApiException("No status code");

        // Assert
        Assert.Null(exception.StatusCode);
    }

    [Fact]
    public void TidalManifestException_Constructor_SetsManifestType()
    {
        // Arrange & Act
        var exception = new TidalManifestException("application/dash+xml", "Manifest parsing failed");

        // Assert
        Assert.Equal("application/dash+xml", exception.ManifestType);
        Assert.Equal("Manifest parsing failed", exception.Message);
        Assert.IsAssignableFrom<TidalException>(exception);
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



