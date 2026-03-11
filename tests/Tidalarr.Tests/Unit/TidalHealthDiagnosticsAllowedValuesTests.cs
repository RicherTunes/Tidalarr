using Lidarr.Plugin.Common.Abstractions.Diagnostics;
using Tidalarr.Diagnostics;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Validates that all DiagnosticHealthResult instances produced by TidalHealthDiagnostics
/// use only well-known, registered error codes, diagnostic types, and capabilities.
/// Prevents "stringly-typed" drift over time.
/// </summary>
public class TidalHealthDiagnosticsAllowedValuesTests
{
    private static readonly HashSet<string> AllowedErrorCodes = new(StringComparer.Ordinal)
    {
        TidalHealthDiagnostics.ErrorCodes.AuthFailed,
        TidalHealthDiagnostics.ErrorCodes.ConnectionFailed,
        TidalHealthDiagnostics.ErrorCodes.ValidationFailed,
    };

    private static readonly HashSet<string> AllowedDiagnosticTypes = new(StringComparer.Ordinal)
    {
        TidalHealthDiagnostics.DiagnosticTypes.AuthValidate,
        TidalHealthDiagnostics.DiagnosticTypes.StreamProbe,
    };

    private static readonly HashSet<string> AllowedCapabilities = new(StringComparer.Ordinal)
    {
        TidalHealthDiagnostics.Capabilities.LosslessDownload,
    };

    private static void AssertAllowedValues(DiagnosticHealthResult result, string context)
    {
        if (result.ErrorCode is not null)
        {
            Assert.True(
                AllowedErrorCodes.Contains(result.ErrorCode),
                $"ErrorCode '{result.ErrorCode}' from {context} must be a registered value. " +
                $"Allowed: [{string.Join(", ", AllowedErrorCodes)}]");
        }

        if (result.DiagnosticType is not null)
        {
            Assert.True(
                AllowedDiagnosticTypes.Contains(result.DiagnosticType),
                $"DiagnosticType '{result.DiagnosticType}' from {context} must be a registered value. " +
                $"Allowed: [{string.Join(", ", AllowedDiagnosticTypes)}]");
        }

        if (result.Capability is not null)
        {
            Assert.True(
                AllowedCapabilities.Contains(result.Capability),
                $"Capability '{result.Capability}' from {context} must be a registered value. " +
                $"Allowed: [{string.Join(", ", AllowedCapabilities)}]");
        }
    }

    [Fact]
    public async Task CheckAuthAsync_Success_UsesOnlyRegisteredValues()
    {
        DiagnosticHealthResult result = await TidalHealthDiagnostics.CheckAuthAsync(
            () => Task.FromResult(true));

        AssertAllowedValues(result, "CheckAuthAsync(success)");
    }

    [Fact]
    public async Task CheckAuthAsync_Failure_UsesOnlyRegisteredValues()
    {
        DiagnosticHealthResult result = await TidalHealthDiagnostics.CheckAuthAsync(
            () => Task.FromResult(false));

        AssertAllowedValues(result, "CheckAuthAsync(failure)");
    }

    [Fact]
    public async Task CheckAuthAsync_Exception_UsesOnlyRegisteredValues()
    {
        DiagnosticHealthResult result = await TidalHealthDiagnostics.CheckAuthAsync(
            () => throw new InvalidOperationException("test"));

        AssertAllowedValues(result, "CheckAuthAsync(exception)");
    }

    [Fact]
    public void CheckStreamAccess_Accessible_UsesOnlyRegisteredValues()
    {
        DiagnosticHealthResult result = TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: true);

        AssertAllowedValues(result, "CheckStreamAccess(accessible)");
    }

    [Fact]
    public void CheckStreamAccess_Inaccessible_UsesOnlyRegisteredValues()
    {
        DiagnosticHealthResult result = TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: false);

        AssertAllowedValues(result, "CheckStreamAccess(inaccessible)");
    }

    [Theory]
    [InlineData("IX000")]
    [InlineData("IX100")]
    [InlineData("IX200")]
    [InlineData("DL000")]
    [InlineData("DL001")]
    [InlineData("DL100")]
    public void FromStableCode_KnownCodes_UsesOnlyRegisteredValues(string code)
    {
        DiagnosticHealthResult result = TidalHealthDiagnostics.FromStableCode(code);

        AssertAllowedValues(result, $"FromStableCode({code})");
    }

    [Fact]
    public void FromStableCode_UnknownCode_PassesCodeAsErrorCode()
    {
        DiagnosticHealthResult result = TidalHealthDiagnostics.FromStableCode("UNKNOWN_CODE");

        // Unknown codes are passed through as the ErrorCode value
        Assert.Equal("UNKNOWN_CODE", result.ErrorCode);
        Assert.False(result.IsHealthy);
    }

    [Fact]
    public void ErrorCodes_AreNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.ErrorCodes.AuthFailed));
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.ErrorCodes.ConnectionFailed));
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.ErrorCodes.ValidationFailed));
    }

    [Fact]
    public void DiagnosticTypes_AreNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.DiagnosticTypes.AuthValidate));
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.DiagnosticTypes.StreamProbe));
    }

    [Fact]
    public void Capabilities_AreNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.Capabilities.LosslessDownload));
    }

    [Fact]
    public void StableCodes_AreNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.StableCodes.IX000));
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.StableCodes.IX100));
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.StableCodes.IX200));
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.StableCodes.DL000));
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.StableCodes.DL001));
        Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.StableCodes.DL100));
    }
}
