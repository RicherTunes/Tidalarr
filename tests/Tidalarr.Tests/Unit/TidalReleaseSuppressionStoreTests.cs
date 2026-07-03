using System;
using System.IO;
using System.Threading.Tasks;
using Tidalarr.Application.Services;
using Tidalarr.Core.Exceptions;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// The Tidal policy adapter over Common's durable <c>TerminalReleaseSuppressionStore</c>. Common owns
/// persistence / TTL / bounds / normalization; this adapter owns only the Tidal-specific "is this reason
/// terminal?" policy. Mirrors qobuz's <c>RestrictedReleaseSuppressionStoreTests</c>.
/// </summary>
public sealed class TidalReleaseSuppressionStoreTests : IDisposable
{
    private readonly string _tempDir;

    public TidalReleaseSuppressionStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "tidalarr-suppression-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    private string NewFilePath([System.Runtime.CompilerServices.CallerMemberName] string testName = "")
        => Path.Combine(_tempDir, testName + ".json");

    [Fact]
    public async Task SuppressAsync_PermanentReason_SuppressesAndPersistsViaCommonStore()
    {
        var path = NewFilePath();
        var first = new TidalReleaseSuppressionStore(path);

        await first.SuppressAsync("  ALBUM-123  ", "track-9", TidalStreamUnavailableReason.RightsRemoved);

        var second = new TidalReleaseSuppressionStore(path);
        Assert.True(second.IsSuppressed("album-123"),
            "Tidal should delegate durable, normalized persistence to Common for terminal restrictions");
    }

    [Theory]
    [InlineData(TidalStreamUnavailableReason.Unknown)]
    [InlineData(TidalStreamUnavailableReason.Authentication)]
    [InlineData(TidalStreamUnavailableReason.Forbidden)]
    [InlineData(TidalStreamUnavailableReason.NotReady)]
    [InlineData(TidalStreamUnavailableReason.RateLimited)]
    [InlineData(TidalStreamUnavailableReason.ServerError)]
    [InlineData(TidalStreamUnavailableReason.Network)]
    public async Task SuppressAsync_NonPermanentReason_DoesNotSuppress(TidalStreamUnavailableReason reason)
    {
        var sut = new TidalReleaseSuppressionStore(NewFilePath());

        await sut.SuppressAsync("album-soft", "track-9", reason);

        Assert.False(sut.IsSuppressed("album-soft"),
            "only an unambiguous rights-removed restriction is terminal — everything else must stay retryable");
        Assert.Equal(0, sut.Count);
    }

    [Fact]
    public async Task SuppressAsync_NullOrWhitespaceAlbumId_IsNoOp()
    {
        var sut = new TidalReleaseSuppressionStore(NewFilePath());

        await sut.SuppressAsync("", "track-9", TidalStreamUnavailableReason.RightsRemoved);
        await sut.SuppressAsync(null!, "track-9", TidalStreamUnavailableReason.RightsRemoved);

        Assert.False(sut.IsSuppressed(""));
        Assert.Equal(0, sut.Count);
    }

    [Fact]
    public async Task ClearAsync_SuppressedAlbum_RemovesSuppressionViaCommonStore()
    {
        var sut = new TidalReleaseSuppressionStore(NewFilePath());
        await sut.SuppressAsync("album-clear", "track-9", TidalStreamUnavailableReason.RightsRemoved);

        var removed = await sut.ClearAsync("album-clear");

        Assert.True(removed);
        Assert.False(sut.IsSuppressed("album-clear"),
            "terminal release suppression must have an explicit clear path for operators / future tooling");
    }

    [Fact]
    public void ShouldSuppress_OnlyPermanentReasonQualifies()
    {
        Assert.True(TidalReleaseSuppressionStore.ShouldSuppress(TidalStreamUnavailableReason.RightsRemoved));
        Assert.False(TidalReleaseSuppressionStore.ShouldSuppress(TidalStreamUnavailableReason.Forbidden));
        Assert.False(TidalReleaseSuppressionStore.ShouldSuppress(TidalStreamUnavailableReason.Unknown));
    }
}
