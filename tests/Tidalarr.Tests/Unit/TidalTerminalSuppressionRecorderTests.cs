using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Tidalarr.Application.Services;
using Tidalarr.Core.Exceptions;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// The download-client side of terminal-release suppression, extracted into the host-free
/// <see cref="TidalTerminalSuppressionRecorder"/> so it runs under the ExcludeHostBridge=true hermetic CI
/// build. After a FAILED album download, if a permanent (terminal) per-track restriction was observed, the
/// album id is recorded in the store. The completion contract (an incomplete album still reports Failed to
/// Lidarr) is a pure download-client concern and is guarded separately in
/// <c>TidalLidarrDownloadClientGetItemsTests</c> (host-coupled).
/// </summary>
public sealed class TidalTerminalSuppressionRecorderTests
{
    [Fact]
    public async Task TryRecord_WithTerminalRestriction_SuppressesAlbumId()
    {
        var store = new RecordingStore();
        var terminals = new[] { new TidalTerminalRestriction("track-7", TidalStreamUnavailableReason.RightsRemoved) };

        await TidalTerminalSuppressionRecorder.TryRecordAsync(
            store, "album-42", terminals, LogManager.GetCurrentClassLogger());

        Assert.Equal("album-42", store.LastAlbumId);
        Assert.Equal("track-7", store.LastTrackId);
        Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, store.LastReason);
    }

    [Fact]
    public async Task TryRecord_WithNoTerminalRestriction_DoesNotSuppress()
    {
        var store = new RecordingStore();

        await TidalTerminalSuppressionRecorder.TryRecordAsync(
            store, "album-42", Array.Empty<TidalTerminalRestriction>(), LogManager.GetCurrentClassLogger());

        Assert.Null(store.LastAlbumId);
    }

    [Fact]
    public async Task TryRecord_WithBlankAlbumId_DoesNotSuppress()
    {
        var store = new RecordingStore();
        var terminals = new[] { new TidalTerminalRestriction("track-7", TidalStreamUnavailableReason.RightsRemoved) };

        await TidalTerminalSuppressionRecorder.TryRecordAsync(
            store, "  ", terminals, LogManager.GetCurrentClassLogger());

        Assert.Null(store.LastAlbumId);
    }

    [Fact]
    public async Task TryRecord_StoreThrows_DoesNotPropagate()
    {
        // Suppression is best-effort: a store failure must never mask / replace the original download
        // failure the caller is already reporting.
        var store = new ThrowingStore();
        var terminals = new[] { new TidalTerminalRestriction("track-7", TidalStreamUnavailableReason.RightsRemoved) };

        var ex = await Record.ExceptionAsync(() => TidalTerminalSuppressionRecorder.TryRecordAsync(
            store, "album-42", terminals, LogManager.GetCurrentClassLogger()));

        Assert.Null(ex);
    }

    private sealed class RecordingStore : ITidalReleaseSuppressionStore
    {
        public string? LastAlbumId { get; private set; }
        public string? LastTrackId { get; private set; }
        public TidalStreamUnavailableReason? LastReason { get; private set; }

        public bool IsSuppressed(string albumId) => false;

        public Task SuppressAsync(string albumId, string trackId, TidalStreamUnavailableReason reason, CancellationToken cancellationToken = default)
        {
            LastAlbumId = albumId;
            LastTrackId = trackId;
            LastReason = reason;
            return Task.CompletedTask;
        }

        public Task<bool> ClearAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class ThrowingStore : ITidalReleaseSuppressionStore
    {
        public bool IsSuppressed(string albumId) => false;
        public Task SuppressAsync(string albumId, string trackId, TidalStreamUnavailableReason reason, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("store down");
        public Task<bool> ClearAsync(string albumId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
