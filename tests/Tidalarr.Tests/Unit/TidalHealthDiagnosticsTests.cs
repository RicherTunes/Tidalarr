using System;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Abstractions.Diagnostics;
using Tidalarr.Diagnostics;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Tests for <see cref="TidalHealthDiagnostics"/> which maps existing
/// stable diagnostic codes (IX000/IX100/IX200/DL000/DL001/DL100) to
/// the Common <see cref="DiagnosticHealthResult"/> shape.
/// </summary>
public class TidalHealthDiagnosticsTests
{
    #region CheckAuthAsync

    [Fact]
    public async Task CheckAuthAsync_Authenticated_ReturnsHealthy()
    {
        var result = await TidalHealthDiagnostics.CheckAuthAsync(() => Task.FromResult(true));

        Assert.True(result.IsHealthy);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("oauth", result.AuthMethod);
        Assert.Equal("auth_validate", result.DiagnosticType);
        Assert.Equal("lossless_download", result.Capability);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.StatusMessage);
    }

    [Fact]
    public async Task CheckAuthAsync_Authenticated_PopulatesResponseTime()
    {
        var result = await TidalHealthDiagnostics.CheckAuthAsync(() => Task.FromResult(true));

        Assert.NotNull(result.ResponseTime);
        Assert.True(result.ResponseTime!.Value >= TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckAuthAsync_NotAuthenticated_ReturnsUnhealthyWithAuthFailed()
    {
        var result = await TidalHealthDiagnostics.CheckAuthAsync(() => Task.FromResult(false));

        Assert.False(result.IsHealthy);
        Assert.Equal("AUTH_FAILED", result.ErrorCode);
        Assert.Equal("Authentication failed", result.StatusMessage);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("oauth", result.AuthMethod);
        Assert.Equal("auth_validate", result.DiagnosticType);
        Assert.Equal("lossless_download", result.Capability);
    }

    [Fact]
    public async Task CheckAuthAsync_NotAuthenticated_PopulatesResponseTime()
    {
        var result = await TidalHealthDiagnostics.CheckAuthAsync(() => Task.FromResult(false));

        Assert.NotNull(result.ResponseTime);
        Assert.True(result.ResponseTime!.Value >= TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckAuthAsync_ThrowsException_ReturnsUnhealthyWithConnectionFailed()
    {
        var result = await TidalHealthDiagnostics.CheckAuthAsync(
            () => throw new InvalidOperationException("Network error"));

        Assert.False(result.IsHealthy);
        Assert.Equal("CONNECTION_FAILED", result.ErrorCode);
        Assert.Equal("Network error", result.StatusMessage);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("oauth", result.AuthMethod);
        Assert.Equal("auth_validate", result.DiagnosticType);
    }

    [Fact]
    public async Task CheckAuthAsync_ThrowsException_PopulatesResponseTime()
    {
        var result = await TidalHealthDiagnostics.CheckAuthAsync(
            () => throw new InvalidOperationException("Timeout"));

        Assert.NotNull(result.ResponseTime);
        Assert.True(result.ResponseTime!.Value >= TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckAuthAsync_OperationCancelled_Rethrows()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => TidalHealthDiagnostics.CheckAuthAsync(
                () => throw new OperationCanceledException()));
    }

    #endregion

    #region CheckStreamAccess

    [Fact]
    public void CheckStreamAccess_Accessible_ReturnsHealthy()
    {
        var result = TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: true);

        Assert.True(result.IsHealthy);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("oauth", result.AuthMethod);
        Assert.Equal("stream_probe", result.DiagnosticType);
        Assert.Equal("lossless_download", result.Capability);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.StatusMessage);
    }

    [Fact]
    public void CheckStreamAccess_Accessible_WithElapsed_SetsResponseTime()
    {
        var elapsed = TimeSpan.FromMilliseconds(250);
        var result = TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: true, elapsed: elapsed);

        Assert.Equal(elapsed, result.ResponseTime);
    }

    [Fact]
    public void CheckStreamAccess_Inaccessible_ReturnsUnhealthy()
    {
        var result = TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: false);

        Assert.False(result.IsHealthy);
        Assert.Equal("CONNECTION_FAILED", result.ErrorCode);
        Assert.Equal("Stream chunks not accessible", result.StatusMessage);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("stream_probe", result.DiagnosticType);
        Assert.Equal("lossless_download", result.Capability);
    }

    [Fact]
    public void CheckStreamAccess_Inaccessible_WithCustomError_UsesCustomMessage()
    {
        var result = TidalHealthDiagnostics.CheckStreamAccess(
            chunksAccessible: false,
            errorDetail: "Geo-blocked region");

        Assert.False(result.IsHealthy);
        Assert.Equal("Geo-blocked region", result.StatusMessage);
    }

    [Fact]
    public void CheckStreamAccess_Inaccessible_WithElapsed_SetsResponseTime()
    {
        var elapsed = TimeSpan.FromSeconds(5);
        var result = TidalHealthDiagnostics.CheckStreamAccess(
            chunksAccessible: false,
            elapsed: elapsed);

        Assert.Equal(elapsed, result.ResponseTime);
    }

    [Fact]
    public void CheckStreamAccess_Inaccessible_DoesNotSetAuthMethod()
    {
        // When chunks are inaccessible, authMethod is not set (per factory call)
        var result = TidalHealthDiagnostics.CheckStreamAccess(chunksAccessible: false);

        Assert.Null(result.AuthMethod);
    }

    #endregion

    #region FromStableCode - Success codes

    [Fact]
    public void FromStableCode_IX000_ReturnsHealthyAuthValidate()
    {
        var result = TidalHealthDiagnostics.FromStableCode("IX000");

        Assert.True(result.IsHealthy);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("oauth", result.AuthMethod);
        Assert.Equal("auth_validate", result.DiagnosticType);
        Assert.Equal("lossless_download", result.Capability);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void FromStableCode_DL000_ReturnsHealthyStreamProbe()
    {
        var result = TidalHealthDiagnostics.FromStableCode("DL000");

        Assert.True(result.IsHealthy);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("oauth", result.AuthMethod);
        Assert.Equal("stream_probe", result.DiagnosticType);
        Assert.Equal("lossless_download", result.Capability);
        Assert.Null(result.ErrorCode);
    }

    #endregion

    #region FromStableCode - Failure codes

    [Fact]
    public void FromStableCode_IX100_ReturnsUnhealthyValidationFailed()
    {
        var result = TidalHealthDiagnostics.FromStableCode("IX100");

        Assert.False(result.IsHealthy);
        Assert.Equal("VALIDATION_FAILED", result.ErrorCode);
        Assert.Equal("Settings validation failed", result.StatusMessage);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("auth_validate", result.DiagnosticType);
        Assert.Null(result.AuthMethod); // No auth method for validation errors
    }

    [Fact]
    public void FromStableCode_IX200_ReturnsUnhealthyAuthFailed()
    {
        var result = TidalHealthDiagnostics.FromStableCode("IX200");

        Assert.False(result.IsHealthy);
        Assert.Equal("AUTH_FAILED", result.ErrorCode);
        Assert.Equal("Authentication failed", result.StatusMessage);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("oauth", result.AuthMethod);
        Assert.Equal("auth_validate", result.DiagnosticType);
    }

    [Fact]
    public void FromStableCode_DL001_ReturnsUnhealthyConnectionFailed()
    {
        var result = TidalHealthDiagnostics.FromStableCode("DL001");

        Assert.False(result.IsHealthy);
        Assert.Equal("CONNECTION_FAILED", result.ErrorCode);
        Assert.Equal("First chunk not accessible", result.StatusMessage);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("stream_probe", result.DiagnosticType);
        Assert.Equal("lossless_download", result.Capability);
    }

    [Fact]
    public void FromStableCode_DL100_ReturnsUnhealthyConnectionFailed()
    {
        var result = TidalHealthDiagnostics.FromStableCode("DL100");

        Assert.False(result.IsHealthy);
        Assert.Equal("CONNECTION_FAILED", result.ErrorCode);
        Assert.Equal("Stream info retrieval failed", result.StatusMessage);
        Assert.Equal("tidal", result.Provider);
        Assert.Equal("stream_probe", result.DiagnosticType);
    }

    #endregion

    #region FromStableCode - Unknown codes

    [Fact]
    public void FromStableCode_UnknownCode_ReturnsUnhealthyWithCodeAsErrorCode()
    {
        var result = TidalHealthDiagnostics.FromStableCode("ZZ999");

        Assert.False(result.IsHealthy);
        Assert.Equal("ZZ999", result.ErrorCode);
        Assert.Equal("Unknown diagnostic code: ZZ999", result.StatusMessage);
        Assert.Equal("tidal", result.Provider);
    }

    [Fact]
    public void FromStableCode_UnknownCode_WithCustomMessage_UsesCustomMessage()
    {
        var result = TidalHealthDiagnostics.FromStableCode("ZZ999", message: "Custom error detail");

        Assert.False(result.IsHealthy);
        Assert.Equal("ZZ999", result.ErrorCode);
        Assert.Equal("Custom error detail", result.StatusMessage);
    }

    #endregion

    #region FromStableCode - Message override

    [Fact]
    public void FromStableCode_IX100_WithCustomMessage_OverridesDefault()
    {
        var result = TidalHealthDiagnostics.FromStableCode("IX100", message: "Market field is empty");

        Assert.False(result.IsHealthy);
        Assert.Equal("VALIDATION_FAILED", result.ErrorCode);
        Assert.Equal("Market field is empty", result.StatusMessage);
    }

    [Fact]
    public void FromStableCode_IX000_WithElapsed_SetsResponseTime()
    {
        var elapsed = TimeSpan.FromMilliseconds(42);
        var result = TidalHealthDiagnostics.FromStableCode("IX000", elapsed: elapsed);

        Assert.True(result.IsHealthy);
        Assert.Equal(elapsed, result.ResponseTime);
    }

    [Fact]
    public void FromStableCode_DL001_WithElapsed_SetsResponseTime()
    {
        var elapsed = TimeSpan.FromSeconds(3);
        var result = TidalHealthDiagnostics.FromStableCode("DL001", elapsed: elapsed);

        Assert.False(result.IsHealthy);
        Assert.Equal(elapsed, result.ResponseTime);
    }

    #endregion

    #region Provider consistency

    [Theory]
    [InlineData("IX000")]
    [InlineData("IX100")]
    [InlineData("IX200")]
    [InlineData("DL000")]
    [InlineData("DL001")]
    [InlineData("DL100")]
    public void FromStableCode_AllCodes_HaveProviderTidal(string code)
    {
        var result = TidalHealthDiagnostics.FromStableCode(code);
        Assert.Equal("tidal", result.Provider);
    }

    #endregion
}
