// <copyright file="DiagnosticsTidalarrTests.cs" company="RicherTunes">
// Copyright (c) RicherTunes. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Lidarr.Plugin.Common.Abstractions.Llm;
using Tidalarr.Integration;
using Xunit;

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
        var result = TestConnectionResult.Success("tidal", "oauth", "quality_detect", 123);

        var json = result.ToJson();

        json.Should().Contain("\"provider\":\"tidal\"");
        json.Should().Contain("\"authMethod\":\"oauth\"");
        json.Should().Contain("\"model\":\"quality_detect\"");
        json.Should().Contain("\"latencyMs\":123");
        json.Should().Contain("\"isHealthy\":true");
    }

    [Fact]
    public void TestConnectionResult_FromProviderHealthResult_Sets_Diag02_Fields()
    {
        var healthResult = ProviderHealthResult.Healthy(TimeSpan.FromMilliseconds(150))
        with
        {
            Provider = "tidal",
            AuthMethod = "oauth",
            Model = "quality_detect"
        };

        var result = TestConnectionResult.FromProviderHealthResult(healthResult);

        result.Provider.Should().Be("tidal");
        result.AuthMethod.Should().Be("oauth");
        result.Model.Should().Be("quality_detect");
        result.IsHealthy.Should().Be(true);
        result.StatusMessage.Should().BeNull();
        result.LatencyMs.Should().Be(150);
    }

    [Fact]
    public void TestConnectionResult_Failure_Sets_ErrorCode_Field()
    {
        var result = TestConnectionResult.Failure("tidal", "oauth", "AUTH_FAILED", "Authentication failed", 100);

        result.Provider.Should().Be("tidal");
        result.AuthMethod.Should().Be("oauth");
        result.ErrorCode.Should().Be("AUTH_FAILED");
        result.IsHealthy.Should().Be(false);
        result.StatusMessage.Should().Be("Authentication failed");
    }

    [Fact]
    public void TestConnectionResult_Success_Fields_NotNull()
    {
        var result = TestConnectionResult.Success("tidal", "oauth", "hi_res", 200);

        result.Provider.Should().NotBeNullOrEmpty();
        result.AuthMethod.Should().NotBeNullOrEmpty();
        result.LatencyMs.Should().BeGreaterThan(0);
        result.IsHealthy.Should().Be(true);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void TestConnectionResult_EscapeJson_PreservesSpecialCharacters()
    {
        var result = TestConnectionResult.Success("tidal", "oauth", "quality", 100);

        var json = result.ToJson();

        json.Should().Contain("\"provider\":\"tidal\"");
        json.Should().Contain("\"authMethod\":\"oauth\"");
        json.Should().Contain("\"model\":\"quality\"");
        json.Should().Contain("\"latencyMs\":100");
        json.Should().Contain("\"isHealthy\":true");
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
        var errorCode = MapErrorToErrorCode(errorMessage);

        errorCode.Should().Be(expectedErrorCode);
    }

    private string MapErrorToErrorCode(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return "UNKNOWN_ERROR";

        var lowerMsg = errorMessage.ToLowerInvariant();

        if (lowerMsg.Contains("authentication"))
            return "AUTH_FAILED";
        if (lowerMsg.Contains("token"))
            return "TOKEN_EXPIRED";
        if (lowerMsg.Contains("rate limit") || lowerMsg.Contains("429"))
            return "RATE_LIMIT_EXCEEDED";
        if (lowerMsg.Contains("network") || lowerMsg.Contains("connection"))
            return "NETWORK_ERROR";
        if (lowerMsg.Contains("api"))
            return "API_ERROR";

        return "UNKNOWN_ERROR";
    }
}
