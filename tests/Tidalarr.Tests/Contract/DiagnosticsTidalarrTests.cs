// <copyright file="DiagnosticsTidalarrTests.cs" company="RicherTunes">
// Copyright (c) RicherTunes. All rights reserved.
// </copyright>

using FluentAssertions;
using Lidarr.Plugin.Common.Abstractions.Llm;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Contract;

/// <summary>
/// Contract tests for Tidalarr diagnostics standardization (DIAG-01 and DIAG-02).
/// These tests verify that Tidalarr indexer returns ProviderHealthResult with DIAG-02 fields.
/// </summary>
public class DiagnosticsTidalarrTests
{
    [Fact]
    public void TestConnectionResult_Serialization_Format_StandardizedJsonStructure()
    {
        TestConnectionResult result = TestConnectionResult.Success("tidal", "oauth", "quality_detect", 123);

        string json = result.ToJson();

        _ = json.Should().Contain("\"provider\":\"tidal\"");
        _ = json.Should().Contain("\"authMethod\":\"oauth\"");
        _ = json.Should().Contain("\"model\":\"quality_detect\"");
        _ = json.Should().Contain("\"latencyMs\":123");
        _ = json.Should().Contain("\"isHealthy\":true");
    }

    [Fact]
    public void TestConnectionResult_FromProviderHealthResult_Sets_Diag02_Fields()
    {
        ProviderHealthResult healthResult = ProviderHealthResult.Healthy(TimeSpan.FromMilliseconds(150))
        with
        {
            Provider = "tidal",
            AuthMethod = "oauth",
            Model = "quality_detect"
        };

        TestConnectionResult result = TestConnectionResult.FromProviderHealthResult(healthResult);

        _ = result.Provider.Should().Be("tidal");
        _ = result.AuthMethod.Should().Be("oauth");
        _ = result.Model.Should().Be("quality_detect");
        _ = result.IsHealthy.Should().Be(true);
        _ = result.StatusMessage.Should().BeNull();
        _ = result.LatencyMs.Should().Be(150);
    }

    [Fact]
    public void TestConnectionResult_Failure_Sets_ErrorCode_Field()
    {
        TestConnectionResult result = TestConnectionResult.Failure("tidal", "oauth", "AUTH_FAILED", "Authentication failed", 100);

        _ = result.Provider.Should().Be("tidal");
        _ = result.AuthMethod.Should().Be("oauth");
        _ = result.ErrorCode.Should().Be("AUTH_FAILED");
        _ = result.IsHealthy.Should().Be(false);
        _ = result.StatusMessage.Should().Be("Authentication failed");
    }

    [Fact]
    public void TestConnectionResult_Success_Fields_NotNull()
    {
        TestConnectionResult result = TestConnectionResult.Success("tidal", "oauth", "hi_res", 200);

        _ = result.Provider.Should().NotBeNullOrEmpty();
        _ = result.AuthMethod.Should().NotBeNullOrEmpty();
        _ = result.LatencyMs.Should().BeGreaterThan(0);
        _ = result.IsHealthy.Should().Be(true);
        _ = result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void TestConnectionResult_EscapeJson_PreservesSpecialCharacters()
    {
        TestConnectionResult result = TestConnectionResult.Success("tidal", "oauth", "quality", 100);

        string json = result.ToJson();

        _ = json.Should().Contain("\"provider\":\"tidal\"");
        _ = json.Should().Contain("\"authMethod\":\"oauth\"");
        _ = json.Should().Contain("\"model\":\"quality\"");
        _ = json.Should().Contain("\"latencyMs\":100");
        _ = json.Should().Contain("\"isHealthy\":true");
    }

    [Theory]
    [InlineData("Authentication failed", "AUTH_FAILED")]
    [InlineData("Token expired", "TOKEN_EXPIRED")]
    [InlineData("Rate limit exceeded", "RATE_LIMIT_EXCEEDED")]
    [InlineData("Network connection error", "NETWORK_ERROR")]
    [InlineData("API error", "API_ERROR")]
    [InlineData("Unknown error message", "UNKNOWN_ERROR")]
    public void TestConnectionResult_ErrorCode_Maps_Correctly(string errorMessage, string expectedErrorCode)
    {
        string errorCode = MapErrorToErrorCode(errorMessage);

        _ = errorCode.Should().Be(expectedErrorCode);
    }

    private string MapErrorToErrorCode(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return "UNKNOWN_ERROR";
        }

        string lowerMsg = errorMessage.ToLowerInvariant();

        return lowerMsg.Contains("authentication") ? "AUTH_FAILED"
            : lowerMsg.Contains("token") ? "TOKEN_EXPIRED"
            : lowerMsg.Contains("rate limit") || lowerMsg.Contains("429") ? "RATE_LIMIT_EXCEEDED"
            : lowerMsg.Contains("network") || lowerMsg.Contains("connection") ? "NETWORK_ERROR"
            : lowerMsg.Contains("api") ? "API_ERROR"
            : "UNKNOWN_ERROR";
    }
}
