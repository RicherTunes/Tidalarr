using System.Diagnostics;
using Lidarr.Plugin.Common.Abstractions.Diagnostics;
using Codes = Lidarr.Plugin.Common.Abstractions.Diagnostics.DiagnosticErrorCodes;

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
    /// Delegates to <see cref="Codes"/> for ecosystem-wide parity.
    /// Local alias kept to minimize downstream churn.
    /// </summary>
    public static class ErrorCodes
    {
        public const string AuthFailed = Codes.AuthFailed;
        public const string ConnectionFailed = Codes.ConnectionFailed;
        public const string ValidationFailed = Codes.ValidationFailed;
    }

    /// <summary>
    /// Well-known diagnostic types emitted by Tidal diagnostics.
    /// </summary>
    public static class DiagnosticTypes
    {
        public const string AuthValidate = "auth_validate";
        public const string StreamProbe = "stream_probe";
    }

    /// <summary>
    /// Well-known capabilities reported by Tidal diagnostics.
    /// </summary>
    public static class Capabilities
    {
        public const string LosslessDownload = "lossless_download";
    }

    /// <summary>
    /// Stable diagnostic codes used for legacy mapping.
    /// </summary>
    public static class StableCodes
    {
        public const string IX000 = "IX000";
        public const string IX100 = "IX100";
        public const string IX200 = "IX200";
        public const string DL000 = "DL000";
        public const string DL001 = "DL001";
        public const string DL100 = "DL100";
    }

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
                    diagnosticType: DiagnosticTypes.AuthValidate,
                    capability: Capabilities.LosslessDownload)
                : DiagnosticHealthResult.Unhealthy(
                    "Authentication failed",
                    responseTime: sw.Elapsed,
                    provider: ProviderName,
                    authMethod: AuthMethodName,
                    diagnosticType: DiagnosticTypes.AuthValidate,
                    capability: Capabilities.LosslessDownload,
                    errorCode: ErrorCodes.AuthFailed);
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
                diagnosticType: DiagnosticTypes.AuthValidate,
                errorCode: ErrorCodes.ConnectionFailed);
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
                diagnosticType: DiagnosticTypes.StreamProbe,
                capability: Capabilities.LosslessDownload)
            : DiagnosticHealthResult.Unhealthy(
                errorDetail ?? "Stream chunks not accessible",
                responseTime: elapsed,
                provider: ProviderName,
                diagnosticType: DiagnosticTypes.StreamProbe,
                capability: Capabilities.LosslessDownload,
                errorCode: ErrorCodes.ConnectionFailed);
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
            StableCodes.IX000 or StableCodes.DL000 => DiagnosticHealthResult.Healthy(
                responseTime: elapsed,
                provider: ProviderName,
                authMethod: AuthMethodName,
                diagnosticType: code.StartsWith("IX", StringComparison.Ordinal) ? DiagnosticTypes.AuthValidate : DiagnosticTypes.StreamProbe,
                capability: Capabilities.LosslessDownload),
            StableCodes.IX100 => DiagnosticHealthResult.Unhealthy(
                message ?? "Settings validation failed",
                responseTime: elapsed,
                provider: ProviderName,
                diagnosticType: DiagnosticTypes.AuthValidate,
                errorCode: ErrorCodes.ValidationFailed),
            StableCodes.IX200 => DiagnosticHealthResult.Unhealthy(
                message ?? "Authentication failed",
                responseTime: elapsed,
                provider: ProviderName,
                authMethod: AuthMethodName,
                diagnosticType: DiagnosticTypes.AuthValidate,
                errorCode: ErrorCodes.AuthFailed),
            StableCodes.DL001 => DiagnosticHealthResult.Unhealthy(
                message ?? "First chunk not accessible",
                responseTime: elapsed,
                provider: ProviderName,
                diagnosticType: DiagnosticTypes.StreamProbe,
                capability: Capabilities.LosslessDownload,
                errorCode: ErrorCodes.ConnectionFailed),
            StableCodes.DL100 => DiagnosticHealthResult.Unhealthy(
                message ?? "Stream info retrieval failed",
                responseTime: elapsed,
                provider: ProviderName,
                diagnosticType: DiagnosticTypes.StreamProbe,
                errorCode: ErrorCodes.ConnectionFailed),
            _ => DiagnosticHealthResult.Unhealthy(
                message ?? $"Unknown diagnostic code: {code}",
                responseTime: elapsed,
                provider: ProviderName,
                errorCode: code),
        };
    }
}
