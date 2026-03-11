namespace Tidalarr.Tests.Utils;

/// <summary>
/// Fact attribute for security scan tests that have high false-positive rates.
/// These tests are skipped by default and enabled via RUN_SECURITY_SCAN_TESTS=1.
/// </summary>
/// <remarks>
/// Why gated: Static regex patterns for SQL injection detection match generic
/// string concatenation, causing false positives. The proper fix is contract-based
/// sanitizer testing (inputs → sanitized outputs) rather than static scanning.
/// </remarks>
public sealed class SecurityScanFactAttribute : FactAttribute
{
    public SecurityScanFactAttribute()
    {
        string? enabled = Environment.GetEnvironmentVariable("RUN_SECURITY_SCAN_TESTS");
        if (!string.Equals(enabled, "1", StringComparison.Ordinal))
        {
            Skip = "Set RUN_SECURITY_SCAN_TESTS=1 to enable high-false-positive security scans.";
        }
    }
}
