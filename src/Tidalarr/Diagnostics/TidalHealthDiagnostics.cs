using System.Diagnostics;
using Lidarr.Plugin.Common.Abstractions.Diagnostics;

namespace Tidalarr.Diagnostics;

/// <summary>
/// Produces structured <see cref="DiagnosticHealthResult"/> for Tidal provider health checks.
/// Maps existing stable diagnostic codes (IX000, IX100, IX200, DL000, DL001, DL100)
/// to the Common DiagnosticHealthResult shape.
/// </summary>
internal static class TidalHealthDiagnostics
{
    private const string ProviderName = "tidal";
    private const string AuthMethodName = "oauth";

    /// <summary>
    /// Performs an authentication health check by invoking the supplied delegate.
    /// </summary>
    /// <param name="isAuthenticated">A delegate that returns <c>true</c> when the user is authenticated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DiagnosticHealthResult"/> reflecting auth status.</returns>
    public static async Task<DiagnosticHealthResult> CheckAuthAsync(
        Func<Task<bool>> isAuthenticated,
        CancellationToken cancellationToken = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            bool authed = await isAuthenticated().ConfigureAwait(false);
            sw.Stop();

            return authed
                ? DiagnosticHealthResult.Healthy(
                    responseTime: sw.Elapsed,
                    provider: ProviderName,
                    authMethod: AuthMethodName,
                    diagnosticType: "auth_validate",
                    capability: "lossless_download")
                : DiagnosticHealthResult.Unhealthy(
                    "Authentication failed",
                    responseTime: sw.Elapsed,
                    provider: ProviderName,
                    authMethod: AuthMethodName,
                    diagnosticType: "auth_validate",
                    capability: "lossless_download",
                    errorCode: "AUTH_FAILED");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return DiagnosticHealthResult.Unhealthy(
                ex.Message,
                responseTime: sw.Elapsed,
                provider: ProviderName,
                authMethod: AuthMethodName,
                diagnosticType: "auth_validate",
                errorCode: "CONNECTION_FAILED");
        }
    }

    /// <summary>
    /// Performs a stream accessibility check.
    /// </summary>
    /// <param name="chunksAccessible">Whether chunks were accessible.</param>
    /// <param name="elapsed">Optional elapsed time for the probe.</param>
    /// <param name="errorDetail">Optional error detail when inaccessible.</param>
    /// <returns>A <see cref="DiagnosticHealthResult"/> reflecting stream probe status.</returns>
    public static DiagnosticHealthResult CheckStreamAccess(
        bool chunksAccessible,
        TimeSpan? elapsed = null,
        string? errorDetail = null)
    {
        return chunksAccessible
            ? DiagnosticHealthResult.Healthy(
                responseTime: elapsed,
                provider: ProviderName,
                authMethod: AuthMethodName,
                diagnosticType: "stream_probe",
                capability: "lossless_download")
            : DiagnosticHealthResult.Unhealthy(
                errorDetail ?? "Stream chunks not accessible",
                responseTime: elapsed,
                provider: ProviderName,
                diagnosticType: "stream_probe",
                capability: "lossless_download",
                errorCode: "CONNECTION_FAILED");
    }

    /// <summary>
    /// Creates a <see cref="DiagnosticHealthResult"/> from an existing stable error code.
    /// Maps IX000/DL000 to Healthy, and IX100/IX200/DL001/DL100 to Unhealthy with appropriate error codes.
    /// </summary>
    /// <param name="code">The stable diagnostic code (e.g., IX000, IX100, IX200, DL000, DL001, DL100).</param>
    /// <param name="message">Optional message override.</param>
    /// <param name="elapsed">Optional response time.</param>
    /// <returns>A <see cref="DiagnosticHealthResult"/> matching the stable code semantics.</returns>
    public static DiagnosticHealthResult FromStableCode(
        string code,
        string? message = null,
        TimeSpan? elapsed = null)
    {
        return code switch
        {
            "IX000" or "DL000" => DiagnosticHealthResult.Healthy(
                responseTime: elapsed,
                provider: ProviderName,
                authMethod: AuthMethodName,
                diagnosticType: code.StartsWith("IX", StringComparison.Ordinal) ? "auth_validate" : "stream_probe",
                capability: "lossless_download"),
            "IX100" => DiagnosticHealthResult.Unhealthy(
                message ?? "Settings validation failed",
                responseTime: elapsed,
                provider: ProviderName,
                diagnosticType: "auth_validate",
                errorCode: "VALIDATION_FAILED"),
            "IX200" => DiagnosticHealthResult.Unhealthy(
                message ?? "Authentication failed",
                responseTime: elapsed,
                provider: ProviderName,
                authMethod: AuthMethodName,
                diagnosticType: "auth_validate",
                errorCode: "AUTH_FAILED"),
            "DL001" => DiagnosticHealthResult.Unhealthy(
                message ?? "First chunk not accessible",
                responseTime: elapsed,
                provider: ProviderName,
                diagnosticType: "stream_probe",
                capability: "lossless_download",
                errorCode: "CONNECTION_FAILED"),
            "DL100" => DiagnosticHealthResult.Unhealthy(
                message ?? "Stream info retrieval failed",
                responseTime: elapsed,
                provider: ProviderName,
                diagnosticType: "stream_probe",
                errorCode: "CONNECTION_FAILED"),
            _ => DiagnosticHealthResult.Unhealthy(
                message ?? $"Unknown diagnostic code: {code}",
                responseTime: elapsed,
                provider: ProviderName,
                errorCode: code),
        };
    }
}
