// <copyright file="TestConnectionResult.cs" company="RicherTunes">
// Copyright (c) RicherTunes. All rights reserved.
// </copyright>

using Lidarr.Plugin.Common.Abstractions.Llm;

namespace Tidalarr.Integration;

/// <summary>
/// Adapter class for Test Connection results that provides standardized JSON structure.
/// Implements DIAG-01 (standardized JSON structure) and DIAG-02 (extended fields).
/// </summary>
public sealed class TestConnectionResult
{
    /// <summary>
    /// Provider identifier (e.g., "tidal")
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Authentication method used (e.g., "oauth", "api_key")
    /// </summary>
    public string AuthMethod { get; set; } = string.Empty;

    /// <summary>
    /// Streaming service model identifier
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Response time in milliseconds
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Whether the connection is healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Status message describing the health state
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Error code if not healthy (DIAG-02)
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Full error details (for serialization)
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Creates a successful test connection result
    /// </summary>
    public static TestConnectionResult Success(string provider, string authMethod, string model, long latencyMs)
    {
        return new TestConnectionResult
        {
            Provider = provider,
            AuthMethod = authMethod,
            Model = model,
            LatencyMs = latencyMs,
            IsHealthy = true,
            StatusMessage = "Connection successful",
            ErrorCode = null
        };
    }

    /// <summary>
    /// Creates a failed test connection result
    /// </summary>
    public static TestConnectionResult Failure(string provider, string authMethod, string errorCode, string message, long latencyMs, string? details = null)
    {
        return new TestConnectionResult
        {
            Provider = provider,
            AuthMethod = authMethod,
            Model = null,
            LatencyMs = latencyMs,
            IsHealthy = false,
            StatusMessage = message,
            ErrorCode = errorCode,
            ErrorDetails = details
        };
    }

    /// <summary>
    /// Converts ProviderHealthResult to TestConnectionResult for JSON serialization
    /// </summary>
    public static TestConnectionResult FromProviderHealthResult(ProviderHealthResult result)
    {
        return new TestConnectionResult
        {
            Provider = result.Provider ?? "tidal",
            AuthMethod = result.AuthMethod ?? "unknown",
            Model = result.Model ?? "quality_detect",
            LatencyMs = (long)(result.ResponseTime?.TotalMilliseconds ?? 0),
            IsHealthy = result.IsHealthy,
            StatusMessage = result.StatusMessage,
            ErrorCode = result.ErrorCode
        };
    }

    /// <summary>
    /// Returns JSON string representation for DIAG-01 standardized structure.
    /// Uses System.Text.Json for proper JSON serialization.
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, JsonSerializerOptions);
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
