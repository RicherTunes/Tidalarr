using Lidarr.Plugin.Common.HostBridge;
using Tidalarr.Integration;
#if !EXCLUDE_HOST_BRIDGE
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Download;
using Tidalarr.Integration.LidarrNative;
#endif

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Regression coverage for the Lidarr host RemoveItem path without referencing
/// Lidarr-native types. These tests must compile and run when ExcludeHostBridge=true.
/// </summary>
[Trait("Category", "Unit")]
public class TidalLidarrDownloadClientCancellationTests
{
    private sealed class TestSettings
    {
        public string DownloadPath { get; init; } = "downloads";

        public TestSettings Clone() => new() { DownloadPath = DownloadPath };
    }

#if !EXCLUDE_HOST_BRIDGE
    private sealed class CapturingOrchestrator : SimpleDownloadOrchestrator
    {
        public bool Called;
        public CancellationToken CapturedToken;

        public CapturingOrchestrator()
            : base(
                serviceName: "test",
                httpClient: new HttpClient(),
                getAlbumAsync: _ => Task.FromResult(new StreamingAlbum()),
                getTrackAsync: _ => Task.FromResult(new StreamingTrack()),
                getAlbumTrackIdsAsync: _ => Task.FromResult<IReadOnlyList<string>>(new List<string>()),
                getStreamAsync: (_, __) => Task.FromResult(("url", "flac")))
        {
        }

        public override Task<DownloadResult> DownloadAlbumAsync(
            string albumId, string outputDirectory, StreamingQuality quality,
            IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            Called = true;
            CapturedToken = cancellationToken;
            return Task.FromResult(new DownloadResult { Success = true });
        }
    }
#endif

#if !EXCLUDE_HOST_BRIDGE
    [Fact]
    public async Task StartAlbumDownloadAsync_ForwardsHostCancellationToken()
    {
        var orchestrator = new CapturingOrchestrator();
        using var cts = new CancellationTokenSource();

        await TidalLidarrDownloadClient.StartAlbumDownloadAsync(
            orchestrator, "album-1", "out-dir", quality: null, progress: null, cts.Token);

        Assert.True(orchestrator.Called, "the orchestrator's album download was not invoked");
        Assert.Equal(cts.Token, orchestrator.CapturedToken);
    }
#endif

    [Fact]
    public void HostBridgeDownloadItemStatus_DefinesCancelledTerminalState()
    {
        Assert.True(
            Enum.TryParse("Cancelled", ignoreCase: false, out HostBridgeDownloadItemStatus status),
            "Host bridge tracker state should distinguish cancellation from generic failure.");

        var item = new HostBridgeDownloadItem { DownloadId = "download-1" };

        item.SetStatus(status);

        Assert.Equal(status, item.GetStatus());
    }

    [Fact]
    public void CancellationRegistry_CancelSignalsToken_WithoutRemovingCompletionOwnership()
    {
        var registry = new TidalDownloadCancellationRegistry();

        using var registration = registry.Register("download-1");
        CancellationToken token = registration.Token;

        Assert.False(token.IsCancellationRequested);
        Assert.True(registry.Cancel("download-1"));
        Assert.True(token.IsCancellationRequested);
        Assert.True(registry.Complete("download-1"));
        Assert.False(registry.Cancel("download-1"));
    }

    [Fact]
    public void CancellationRegistry_CancelCallbackException_RemainsBestEffort()
    {
        var registry = new TidalDownloadCancellationRegistry();

        using var registration = registry.Register("download-1");
        using var callback = registration.Token.Register(() => throw new InvalidOperationException("callback failed"));

        Assert.True(registry.Cancel("download-1"));
        Assert.True(registration.Token.IsCancellationRequested);
        Assert.True(registry.Complete("download-1"));
    }

    [Fact]
    public async Task OrchestratorCancellationOptions_AllowsRemovePathToCancelRunningWork()
    {
        var registry = new TidalDownloadCancellationRegistry();
        var orchestrator = new HostBridgeDownloadOrchestrator(logger: null);
        var tracker = new HostBridgeDownloadTrackerStore<HostBridgeDownloadItem>();
        var settings = new TestSettings();
        HostBridgeDownloadItemStatus cancelled = ParseCancelledStatus();
        var workStarted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<string> startTask = orchestrator.StartTrackedDownloadAsync(
            settings: settings,
            tracker: tracker,
            snapshotter: s => s.Clone(),
            itemFactory: (s, downloadId) => new HostBridgeDownloadItem
            {
                DownloadId = downloadId,
                AlbumId = "album-1",
                Title = "Album",
                Artist = "Artist",
                OutputPath = s.DownloadPath
            },
            doWork: async (_, downloadId, item, cancellationToken) =>
            {
                workStarted.TrySetResult(downloadId);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    item.SetStatus(cancelled);
                    item.CompletedAt = DateTime.UtcNow;
                    workCancelled.TrySetResult(true);
                    throw;
                }
            },
            new HostBridgeDownloadStartOptions<HostBridgeDownloadItem>
            {
                RegisterCancellation = (downloadId, _) => registry.Register(downloadId)
            });

        string startedDownloadId = await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        string runningDownloadId = await workStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(startedDownloadId, runningDownloadId);

        Assert.True(registry.Cancel(startedDownloadId));
        Assert.True(await workCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(tracker.TryGet(startedDownloadId, out HostBridgeDownloadItem? item));
        Assert.Equal(cancelled, item!.GetStatus());
        Assert.True(
            await EventuallyAsync(() => !registry.Cancel(startedDownloadId)),
            "Common should dispose the cancellation registration after the cancelled work exits");
    }

    [Fact]
    public void RemovalCoordinator_CancelsAndRemovesTrackedDownload()
    {
        var registry = new TidalDownloadCancellationRegistry();
        var tracker = new HostBridgeDownloadTrackerStore<HostBridgeDownloadItem>();
        using var registration = registry.Register("download-1");
        tracker.AddOrReplace(new HostBridgeDownloadItem { DownloadId = "download-1" });

        var result = TidalDownloadRemovalCoordinator.Remove(
            "download-1",
            deleteData: false,
            tracker,
            registry);

        Assert.True(result.Removed);
        Assert.True(result.CancellationSignaled);
        Assert.Empty(tracker.GetSnapshot());
        Assert.True(registration.Token.IsCancellationRequested);
    }

    [Fact]
    public void RemovalCoordinator_CancelCallbackException_StillRemovesTrackedDownload()
    {
        var registry = new TidalDownloadCancellationRegistry();
        var tracker = new HostBridgeDownloadTrackerStore<HostBridgeDownloadItem>();
        using var registration = registry.Register("download-1");
        using var callback = registration.Token.Register(() => throw new InvalidOperationException("callback failed"));
        tracker.AddOrReplace(new HostBridgeDownloadItem { DownloadId = "download-1" });

        var result = TidalDownloadRemovalCoordinator.Remove(
            "download-1",
            deleteData: false,
            tracker,
            registry);

        Assert.True(result.Removed);
        Assert.True(result.CancellationSignaled);
        Assert.Empty(tracker.GetSnapshot());
        Assert.True(registration.Token.IsCancellationRequested);
    }

    [Fact]
    public void RemovalCoordinator_RemoveWithoutTrackerItem_CompletesCancellationRegistration()
    {
        var registry = new TidalDownloadCancellationRegistry();
        var tracker = new HostBridgeDownloadTrackerStore<HostBridgeDownloadItem>();
        using var registration = registry.Register("download-1");

        var result = TidalDownloadRemovalCoordinator.Remove(
            "download-1",
            deleteData: false,
            tracker,
            registry);

        Assert.False(result.Removed);
        Assert.True(result.CancellationSignaled);
        Assert.True(registration.Token.IsCancellationRequested);
        Assert.False(registry.Cancel("download-1"));
    }

    private static HostBridgeDownloadItemStatus ParseCancelledStatus()
    {
        Assert.True(Enum.TryParse("Cancelled", out HostBridgeDownloadItemStatus cancelled));
        return cancelled;
    }

    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return condition();
    }
}
