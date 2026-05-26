using System.Collections.Generic;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Abstractions.Diagnostics;
using Lidarr.Plugin.Common.TestKit.Compliance;
using Tidalarr.Diagnostics;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Validates that all DiagnosticHealthResult instances produced by TidalHealthDiagnostics
/// use only well-known, registered error codes, diagnostic types, and capabilities.
/// </summary>
public class TidalHealthDiagnosticsAllowedValuesTests : DiagnosticsAllowedValuesTestBase
{
    protected override IReadOnlySet<string> AllowedErrorCodes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        TidalHealthDiagnostics.ErrorCodes.AuthFailed,
        TidalHealthDiagnostics.ErrorCodes.ConnectionFailed,
        TidalHealthDiagnostics.ErrorCodes.ValidationFailed,
    };

    protected override IReadOnlySet<string> AllowedDiagnosticTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        TidalHealthDiagnostics.DiagnosticTypes.AuthValidate,
        TidalHealthDiagnostics.DiagnosticTypes.StreamProbe,
    };

    protected override IReadOnlySet<string> AllowedCapabilities { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        TidalHealthDiagnostics.Capabilities.LosslessDownload,
    };

    protected override async Task<IEnumerable<DiagnosticHealthResult>> GetHealthResultsAsync()
    {
        var results = new List<DiagnosticHealthResult>
        {
            await TidalHealthDiagnostics.CheckAuthAsync(() => Task.FromResult(true)),
            await TidalHealthDiagnostics.CheckAuthAsync(() => Task.FromResult(false)),
            await TidalHealthDiagnostics.CheckAuthAsync(() => throw new InvalidOperationException("test")),
            TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: true),
            TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: false),
            TidalHealthDiagnostics.FromStableCode("IX000"),
            TidalHealthDiagnostics.FromStableCode("IX100"),
            TidalHealthDiagnostics.FromStableCode("IX200"),
            TidalHealthDiagnostics.FromStableCode("DL000"),
            TidalHealthDiagnostics.FromStableCode("DL001"),
            TidalHealthDiagnostics.FromStableCode("DL100"),
        };
        return results;
    }

    // Per-scenario facts kept for richer failure output
    [Fact]
    public async Task CheckAuthAsync_Success_UsesOnlyRegisteredValues()
        => AssertAllowed(await TidalHealthDiagnostics.CheckAuthAsync(() => Task.FromResult(true)), "CheckAuthAsync(success)");

    [Fact]
    public async Task CheckAuthAsync_Failure_UsesOnlyRegisteredValues()
        => AssertAllowed(await TidalHealthDiagnostics.CheckAuthAsync(() => Task.FromResult(false)), "CheckAuthAsync(failure)");

    [Fact]
    public async Task CheckAuthAsync_Exception_UsesOnlyRegisteredValues()
        => AssertAllowed(await TidalHealthDiagnostics.CheckAuthAsync(() => throw new InvalidOperationException("test")), "CheckAuthAsync(exception)");

    [Fact]
    public void CheckStreamAccess_Accessible_UsesOnlyRegisteredValues()
        => AssertAllowed(TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: true), "CheckStreamAccess(accessible)");

    [Fact]
    public void CheckStreamAccess_Inaccessible_UsesOnlyRegisteredValues()
        => AssertAllowed(TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: false), "CheckStreamAccess(inaccessible)");

    [Theory]
    [InlineData("IX000")]
    [InlineData("IX100")]
    [InlineData("IX200")]
    [InlineData("DL000")]
    [InlineData("DL001")]
    [InlineData("DL100")]
    public void FromStableCode_KnownCodes_UsesOnlyRegisteredValues(string code)
        => AssertAllowed(TidalHealthDiagnostics.FromStableCode(code), $"FromStableCode({code})");

    [Fact]
    public void FromStableCode_UnknownCode_PassesCodeAsErrorCode()
    {
        var result = TidalHealthDiagnostics.FromStableCode("UNKNOWN_CODE");
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
        => Assert.False(string.IsNullOrWhiteSpace(TidalHealthDiagnostics.Capabilities.LosslessDownload));

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
