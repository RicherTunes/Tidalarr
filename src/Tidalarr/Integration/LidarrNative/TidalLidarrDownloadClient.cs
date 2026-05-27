using FluentValidation.Results;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.HostBridge;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Observability;
using Lidarr.Plugin.Common.Services.Authentication;
using Lidarr.Plugin.Common.Services.Bridge;
using Lidarr.Plugin.Common.Services.Diagnostics;
using Lidarr.Plugin.Common.Services.Download;
using Lidarr.Plugin.Common.Utilities;
using Lidarr.Plugin.Common.Validation;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Lidarr-native download client extending DownloadClientBase for plugin discovery.
/// Lidarr's plugin system scans for classes extending this base class.
/// Uses TidalModule services internally for actual download functionality.
/// </summary>
public class TidalLidarrDownloadClient(
    IConfigService configService,
    IDiskProvider diskProvider,
    IRemotePathMappingService remotePathMappingService,
    ILocalizationService localizationService,
    Logger logger) : DownloadClientBase<TidalLidarrDownloadClientSettings>(configService, diskProvider, remotePathMappingService, localizationService, logger), IDisposable
{
    // IMPORTANT: Lidarr may construct plugin types more than once. Download tracking must be
    // process-wide so queue polling always sees active downloads, even if a new instance is created.
    // HostBridgeDownloadTrackerStore is instance-scoped but held in a static field for exactly this reason.
    private static readonly HostBridgeDownloadTrackerStore<HostBridgeDownloadItem> ActiveDownloads = new();
    private static readonly HostBridgeDownloadOrchestrator _downloadOrchestrator = new(logger: null);
    private new readonly Logger _logger = logger;

    public override string Name => "Tidalarr";
    public override string Protocol => nameof(TidalarrDownloadProtocol);

    /// <summary>
    /// Resolve (or lazily build) the runtime for the current settings via the process-wide
    /// <see cref="TidalDownloadClientRuntimeCache"/>. Returns null when ConfigPath is empty.
    /// Replaces the old double-checked-lock <c>EnsureServicesInitialized()</c> pattern,
    /// gaining automatic invalidation when ConfigPath changes.
    /// </summary>
    private Task<TidalDownloadClientRuntime?> GetRuntimeAsync(CancellationToken ct = default)
        => TidalDownloadClientRuntimeCache.Shared.GetAsync(Settings, ct);

    /// <summary>
    /// Synchronous shim for callers in sync Lidarr host-contract methods (Test, GetParser).
    /// DownloadClientBase.Test(List&lt;ValidationFailure&gt;) is a protected void — no async override exists
    /// in this version of Lidarr's plugins branch. Task.Run avoids deadlock when a
    /// SynchronizationContext captures the calling thread. Credential-change invalidation still
    /// fires through the cache.
    /// </summary>
    private TidalDownloadClientRuntime? EnsureServicesInitialized()
    {
        TidalDownloadClientRuntime? runtime = Task.Run(() => GetRuntimeAsync()).GetAwaiter().GetResult();
        if (runtime is null)
        {
            this._logger.Warn("Tidal download client runtime not available (ConfigPath empty?)");
        }
        else
        {
            this._logger.Debug("Tidal download services initialized successfully");
        }
        return runtime;
    }

    public override Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
    {
        using PluginLogContext ctx = PluginLogContext.Push("Tidalarr", "Download");
        try
        {
            TidalDownloadClientRuntime? rt = EnsureServicesInitialized();
            if (rt is null)
            {
                throw new InvalidOperationException("Tidal download client runtime unavailable — ConfigPath may be empty.");
            }

            // AuthFailureGate: if the gate is latched bad and no probe slot is available,
            // surface a clear "auth needs attention" failure instead of starting a download
            // that will burn the user's API quota / risk IP-ban on a dead session.
            if (IsAuthShortCircuited(rt.ServiceProvider))
            {
                throw new InvalidOperationException(
                    "Tidal authentication is latched bad (auth failure observed recently). " +
                    "Re-authenticate by pasting a fresh redirect URL and retry — the gate will " +
                    "auto-recover once a request succeeds.");
            }

            string albumTitle = remoteAlbum.Albums?.FirstOrDefault()?.Title ?? "Unknown Album";
            string artistName = remoteAlbum.Artist?.Name ?? "Unknown Artist";

            this._logger.Info("Starting Tidal download: {0} - {1}", artistName, albumTitle);

            // Extract album ID and requested quality from release
            string albumId = ExtractAlbumIdFromRelease(remoteAlbum.Release);
            if (string.IsNullOrWhiteSpace(albumId))
            {
                throw new InvalidOperationException("Could not extract album ID from release");
            }

            TidalQuality? releaseQuality = ExtractQualityFromRelease(remoteAlbum.Release);

            // Resolve services BEFORE snapshotting/Task.Run so failures surface synchronously
            // instead of leaving a phantom "Downloading" item in ActiveDownloads.
            TidalModelMapper mapper = rt.ServiceProvider.GetRequiredService<TidalModelMapper>();
            StreamingQuality desiredQuality = releaseQuality.HasValue
                ? mapper.ToStreamingQuality(releaseQuality.Value)
                : mapper.ToStreamingQuality(Settings.PreferredQuality);

            // Capture orchestrator reference to avoid closure over `this`
            SimpleDownloadOrchestrator tidalOrchestrator = rt.Orchestrator;

            // BuildOutputPath reads Settings.DownloadPath — call it here (before Task.Run) so
            // it is resolved against the current settings before snapshotting. The resulting
            // string is baked into the download item by itemFactory below.
            string outputPath = BuildOutputPath(remoteAlbum);

            // HostBridgeDownloadOrchestrator (Common Wave A item 2):
            //   snapshot → generate downloadId → insert into tracker → fire-and-forget doWork → return id
            //
            // Snapshotter for TidalLidarrDownloadClientSettings: field-by-field copy so the
            // doWork closure is insulated from live settings changes (ProbeOnly-race pattern).
            // Reference-typed fields: none in this settings class — all primitives/strings.
            return _downloadOrchestrator.StartTrackedDownloadAsync<HostBridgeDownloadItem, TidalLidarrDownloadClientSettings>(
                settings: Settings,
                tracker: ActiveDownloads,
                snapshotter: s => new TidalLidarrDownloadClientSettings
                {
                    ConfigPath = s.ConfigPath,
                    DownloadPath = s.DownloadPath,
                    PreferredQuality = s.PreferredQuality,
                    IncludeMqa = s.IncludeMqa,
                    ExtractFlac = s.ExtractFlac,
                    DownloadDelay = s.DownloadDelay,
                    MaxConcurrentTrackDownloads = s.MaxConcurrentTrackDownloads,
                    MaxConcurrentChunkDownloads = s.MaxConcurrentChunkDownloads
                },
                itemFactory: (_, downloadId) =>
                {
                    HostBridgeDownloadItem item = new()
                    {
                        DownloadId = downloadId,
                        AlbumId = albumId,
                        Title = albumTitle,
                        Artist = artistName,
                        OutputPath = outputPath,
                        StartedAt = DateTime.UtcNow
                    };
                    item.SetStatus(HostBridgeDownloadItemStatus.Downloading);
                    item.SetProgress(0);
                    return item;
                },
                doWork: async (_, downloadId, _, ct) =>
                {
                    try
                    {
                        this._logger.Debug("Starting async download for album {0}", albumId);

                        // Create progress reporter to update download item
                        Progress<DownloadProgress> progressReporter = new(p =>
                        {
                            if (ActiveDownloads.TryGet(downloadId, out HostBridgeDownloadItem? progressItem) && progressItem is not null)
                            {
                                progressItem.SetProgress(p.PercentComplete);
                            }
                        });

                        DownloadResult result = await tidalOrchestrator.DownloadAlbumAsync(
                            albumId,
                            outputPath,
                            quality: desiredQuality,
                            progress: progressReporter);

                        // Mark as completed (thread-safe updates)
                        if (ActiveDownloads.TryGet(downloadId, out HostBridgeDownloadItem? item) && item is not null)
                        {
                            item.SetStatus(result.Success ? HostBridgeDownloadItemStatus.Completed : HostBridgeDownloadItemStatus.Failed);
                            item.SetProgress(100);
                            item.CompletedAt = DateTime.UtcNow;

                            // Log all track results for debugging
                            List<TrackDownloadResult> failedTracks = [.. result.TrackResults.Where(t => !t.Success)];
                            List<TrackDownloadResult> successTracks = [.. result.TrackResults.Where(t => t.Success)];

                            if (failedTracks.Count > 0 || result.FilePaths?.Count == 0)
                            {
                                this._logger.Error("Download issues for {0} - {1}: {2} failed, {3} succeeded, {4} files on disk",
                                    artistName, albumTitle, failedTracks.Count, successTracks.Count, result.FilePaths?.Count ?? 0);
                                foreach (TrackDownloadResult? tr in failedTracks.Take(5))
                                {
                                    this._logger.Error("  Track {0} failed: {1}", tr.TrackId, tr.ErrorMessage);
                                }
                                if (result.TrackResults.Count == 0)
                                {
                                    this._logger.Error("  No track results at all - likely no track IDs returned from API");
                                }
                            }
                            else
                            {
                                this._logger.Info("Completed download: {0} - {1} ({2} files)", artistName, albumTitle, result.FilePaths?.Count ?? 0);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Best-effort: record auth-class outcomes so the next Download/Test
                        // short-circuits. Captured 'rt' is closed-over from the outer scope so
                        // we don't need another runtime resolution.
                        RecordAuthOutcomeFromException(rt.ServiceProvider, ex);

                        this._logger.Error(ex, "Failed to download album {0}", albumId);
                        if (ActiveDownloads.TryGet(downloadId, out HostBridgeDownloadItem? item) && item is not null)
                        {
                            item.SetStatus(HostBridgeDownloadItemStatus.Failed);
                            item.CompletedAt = DateTime.UtcNow;
                        }
                    }
                });
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Failed to start Tidal download");
            throw;
        }
    }

    public override IEnumerable<DownloadClientItem> GetItems()
    {
        List<DownloadClientItem> result = [];

        // GetSnapshot() evicts completed/failed items past the retention window as a side-effect.
        foreach (HostBridgeDownloadItem item in ActiveDownloads.GetSnapshot())
        {
            HostBridgeDownloadItemStatus status = item.GetStatus();
            double progress = item.GetProgress();

            // Map Common status enum to Lidarr host enum.
            DownloadItemStatus hostStatus = status switch
            {
                HostBridgeDownloadItemStatus.Completed => DownloadItemStatus.Completed,
                HostBridgeDownloadItemStatus.Failed    => DownloadItemStatus.Failed,
                HostBridgeDownloadItemStatus.Downloading => DownloadItemStatus.Downloading,
                _                                      => DownloadItemStatus.Queued
            };

            result.Add(new DownloadClientItem
            {
                DownloadId = item.DownloadId,
                Title = $"{item.Artist} - {item.Title}",
                Status = hostStatus,
                TotalSize = item.TotalSize,
                RemainingSize = item.TotalSize - (long)(item.TotalSize * progress / 100),
                OutputPath = new OsPath(item.OutputPath),
                DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false)
            });
        }

        return result;
    }

    public override void RemoveItem(DownloadClientItem item, bool deleteData)
    {
        if (ActiveDownloads.Remove(item.DownloadId, deleteData, out _))
        {
            this._logger.Debug("Removed Tidal download: {0}", item.DownloadId);
        }
    }

    public override DownloadClientInfo GetStatus()
    {
        return new DownloadClientInfo
        {
            IsLocalhost = true,
            OutputRootFolders = [new OsPath(Settings.DownloadPath)]
        };
    }

    protected override void Test(List<ValidationFailure> failures)
    {
        using PluginLogContext ctx = PluginLogContext.Push("Tidalarr", "Test");
        try
        {
            this._logger.Info("Testing Tidalarr download client connection...");

            // Basic settings validation — accumulate ALL missing-field failures before
            // returning so the user sees every gap in a single Test click.
            // (Replaces two sequential early-return checks; fixes PR #130 finding #12.)
            var builder = new TestValidationBuilder()
                .RequireNonEmpty("ConfigPath", Settings.ConfigPath, "Config path is required")
                .RequireNonEmpty("DownloadPath", Settings.DownloadPath, "Download path is required");

            failures.AddRange(builder.Build());
            if (builder.HasFailures)
            {
                return;
            }

            var pathValidation = Lidarr.Plugin.Common.Services.Validation.DownloadPathValidator.Validate(Settings.DownloadPath);
            if (!pathValidation.IsValid)
            {
                failures.Add(new ValidationFailure("DownloadPath", pathValidation.Message));
                return;
            }

            // Verify download path exists or can be created
            if (!Directory.Exists(Settings.DownloadPath))
            {
                try
                {
                    _ = Directory.CreateDirectory(Settings.DownloadPath);
                    this._logger.Debug("Created download directory: {0}", Settings.DownloadPath);
                }
                catch (Exception ex)
                {
                    failures.Add(new ValidationFailure("DownloadPath", $"Cannot create download path: {ex.Message}"));
                    return;
                }
            }

            // Initialize services and test authentication (via cache — invalidates on ConfigPath change).
            TidalDownloadClientRuntime? testRt = EnsureServicesInitialized();
            if (testRt is null)
            {
                failures.Add(new ValidationFailure("ConfigPath", "Tidal runtime could not be initialized — ConfigPath may be empty."));
                return;
            }

            // AuthFailureGate: if the gate is latched bad and no probe slot is available,
            // surface that to the user as a Test failure with an actionable message.
            if (IsAuthShortCircuited(testRt.ServiceProvider))
            {
                failures.Add(new ValidationFailure(
                    "Authentication",
                    "Tidal authentication is latched bad (auth failure observed recently). Re-authenticate by pasting a fresh redirect URL and retry — the gate will auto-recover once a request succeeds."));
                return;
            }

            IStreamingAuthManager? authManager = testRt.ServiceProvider.GetService<IStreamingAuthManager>();
            if (authManager != null)
            {
                try
                {
                    // SYNC-OVER-ASYNC: DownloadClientBase.Test() is a synchronous Lidarr host contract.
                    authManager.EnsureValidSessionAsync().GetAwaiter().GetResult();
                    this._logger.Debug("Tidal authentication session is valid");
                }
                catch (Exception authEx)
                {
                    this._logger.Warn(authEx, "Tidal authentication not configured or invalid");
                    failures.Add(new ValidationFailure("Authentication",
                        "Not authenticated with Tidal. Please complete the OAuth flow using the redirect URL."));
                    return;
                }
            }

            this._logger.Info("Tidalarr download client test completed successfully");
        }
        catch (Exception ex)
        {
            // Best-effort: record auth-class outcomes so subsequent calls short-circuit.
            // testRt may not have been resolved if the failure happened pre-runtime; that's
            // handled by re-fetching it safely here. Recording is opportunistic; never let
            // it mask the original failure.
            try
            {
                TidalDownloadClientRuntime? runtimeForGate = EnsureServicesInitialized();
                if (runtimeForGate is not null)
                {
                    RecordAuthOutcomeFromException(runtimeForGate.ServiceProvider, ex);
                }
            }
            catch
            {
                // Best-effort only.
            }

            this._logger.Error(ex, "Tidalarr download client test failed");
            // HttpExceptionClassifier (Common): actionable hint instead of leaking the CLR
            // exception type. Auth-class failures route to the "Authentication" field so the
            // UI surfaces them in the credential section. Mirrors the indexer pattern.
            HttpFailureClassification classification = HttpExceptionClassifier.Classify(ex);
            string failureField = classification.Category == HttpFailureCategory.Auth
                ? "Authentication"
                : "Test";
            failures.Add(new ValidationFailure(failureField, classification.Hint));
        }
    }

    private static string ExtractAlbumIdFromRelease(ReleaseInfo release)
        => PrefixedReleaseGuidParser.ExtractAlbumId(release?.Guid, release?.InfoUrl, "tidal");

    /// <summary>
    /// Extracts quality from a release's DownloadUrl (?quality=Lossless) or GUID (tidal:album:ID:Quality).
    /// Returns null if no quality is encoded, falling back to user's PreferredQuality setting.
    /// </summary>
    internal static TidalQuality? ExtractQualityFromRelease(ReleaseInfo? release)
    {
        // Try DownloadUrl first: tidal://album/{id}?quality={quality}
        if (!string.IsNullOrWhiteSpace(release?.DownloadUrl) && Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out Uri? uri))
        {
            string? qualityParam = System.Web.HttpUtility.ParseQueryString(uri.Query)["quality"];
            if (Enum.TryParse<TidalQuality>(qualityParam, ignoreCase: true, out TidalQuality q))
            {
                return q;
            }
        }

        // Fallback: parse from GUID 4th segment (tidal:album:ID:Quality)
        if (!string.IsNullOrWhiteSpace(release?.Guid))
        {
            string[] parts = release.Guid.Split(':');
            if (parts.Length >= 4 && Enum.TryParse<TidalQuality>(parts[3], ignoreCase: true, out TidalQuality gq))
            {
                return gq;
            }
        }

        return null;
    }

    private string BuildOutputPath(RemoteAlbum remoteAlbum)
    {
        string basePath = Settings.DownloadPath;
        // PathTraversalGuard.SanitizeSegment collapses pure-dot segments (`..`, `...`) which
        // FileSystemUtilities.SanitizeFileName doesn't fully handle. Adopting it brings tidal's
        // path safety up to apple's PR #130 hardening level (the same exposure exists here:
        // hostile artist/album metadata + Directory.Delete(recursive: true) would escape root).
        string artistName = PathTraversalGuard.SanitizeSegment(remoteAlbum.Artist?.Name ?? "Unknown Artist");
        string albumTitle = PathTraversalGuard.SanitizeSegment(remoteAlbum.Albums?.FirstOrDefault()?.Title ?? "Unknown Album");

        string output = Path.Combine(basePath, artistName, albumTitle);

        // Defense-in-depth: canonical-form containment check. If a future change to sanitize
        // misses a new traversal vector (or an attacker crafts something exotic), refuse to
        // operate on a path outside the configured download root.
        if (!PathTraversalGuard.IsPathWithinRoot(output, basePath))
        {
            throw new InvalidOperationException(
                $"Tidalarr: refusing to build output path '{output}' — resolves outside the configured DownloadPath '{basePath}'.");
        }

        return output;
    }

    public void Dispose()
    {
        // Runtime lifetime is managed by TidalDownloadClientRuntimeCache (graveyard pattern).
        // Instance-level disposal is a no-op; the cache disposes runtimes after the linger window.
    }

    // ------------------------------------------------------------------ //
    // AuthFailureGate helpers
    //
    // Mirror the apple/qobuz pattern (AppleMusicIndexerAdapter.cs:63-104) and the
    // sibling helpers in TidalLidarrIndexer. Per-call IServiceProvider resolution
    // (rather than ctor-injection) is required because Lidarr's DownloadClientBase
    // ctor signature is fixed — adding a param would break discovery.
    // ------------------------------------------------------------------ //

    private static bool IsAuthShortCircuited(IServiceProvider sp)
    {
        AuthFailureGate? gate = sp.GetService<AuthFailureGate>();
        if (gate is null) return false;
        if (gate.IsHealthy) return false;
        return !gate.TryAcquireProbeSlot();
    }

    private static void RecordAuthOutcomeFromException(IServiceProvider sp, Exception ex)
    {
        AuthFailureGate? gate = sp.GetService<AuthFailureGate>();
        if (gate is null) return;
        if (!LooksLikeAuthFailure(ex)) return;

        var failure = new Lidarr.Plugin.Abstractions.Contracts.AuthFailure
        {
            ErrorCode = (ex as System.Net.Http.HttpRequestException)?.StatusCode?.ToString(),
            Message = ex.Message,
        };
        // SYNC-OVER-ASYNC (Category A): thread-pool hop avoids host-context deadlock.
        Task.Run(() => gate.Handler.HandleFailureAsync(failure).AsTask())
            .GetAwaiter().GetResult();
    }

    private static bool LooksLikeAuthFailure(Exception ex)
    {
        if (ex is System.Net.Http.HttpRequestException hre &&
            hre.StatusCode is System.Net.HttpStatusCode.Unauthorized
                           or System.Net.HttpStatusCode.Forbidden)
        {
            return true;
        }
        return false;
    }
}

// TidalDownloadItem removed — replaced by Lidarr.Plugin.Common.HostBridge.HostBridgeDownloadItem
// (Wave A item 1 of the May 2026 unification plan).
