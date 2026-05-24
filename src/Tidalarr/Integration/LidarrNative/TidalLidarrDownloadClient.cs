using FluentValidation.Results;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.HostBridge;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
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
    private new readonly Logger _logger = logger;
    private IServiceProvider _serviceProvider;
    private bool _servicesInitialized;
    private readonly object _initLock = new();
    private SimpleDownloadOrchestrator _orchestrator;

    public override string Name => "Tidalarr";
    public override string Protocol => nameof(TidalarrDownloadProtocol);

    /// <summary>
    /// Initialize Tidal services from TidalModule when first needed.
    /// Thread-safe via double-checked locking to prevent duplicate ServiceProvider builds.
    /// </summary>
    private void EnsureServicesInitialized()
    {
        if (this._servicesInitialized)
        {
            return;
        }

        lock (this._initLock)
        {
            if (this._servicesInitialized)
            {
                return;
            }

            try
            {
                ServiceCollection services = new();

                // Use the ToTidalSettings() method for proper conversion
                TidalDownloadClientSettings downloadSettings = Settings.ToTidalSettings();
                _ = services.AddSingleton(downloadSettings);

                // Create TidalarrSettings from Lidarr-native settings.
                // RedirectUrl is empty - download client uses tokens from shared ConfigPath
                // (authentication is done via the indexer, not the download client).
                TidalarrSettings tidalarrSettings = new()
                {
                    ConfigPath = Settings.ConfigPath,
                    RedirectUrl = string.Empty,
                    DownloadPath = Settings.DownloadPath,
                    PreferredQuality = Settings.PreferredQuality,
                    IncludeMqa = Settings.IncludeMqa,
                    ExtractFlac = Settings.ExtractFlac,
                    DownloadDelay = Settings.DownloadDelay,
                    MaxConcurrentTrackDownloads = Settings.MaxConcurrentTrackDownloads,
                    MaxConcurrentChunkDownloads = Settings.MaxConcurrentChunkDownloads
                };
                _ = services.AddSingleton(tidalarrSettings);

                // Register all Tidal services
                TidalModule.RegisterServices(services);

                this._serviceProvider = services.BuildServiceProvider();
                this._orchestrator = TidalModule.CreateOrchestrator(this._serviceProvider);
                this._servicesInitialized = true;
                this._logger.Debug("Tidal download services initialized successfully");
            }
            catch (Exception ex)
            {
                this._logger.Error(ex, "Failed to initialize Tidal download services");
                throw;
            }
        }
    }

    public override Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
    {
        try
        {
            EnsureServicesInitialized();

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

            // Resolve services BEFORE Task.Run so failures surface synchronously
            // instead of leaving a phantom "Downloading" item in ActiveDownloads.
            TidalModelMapper mapper = this._serviceProvider.GetRequiredService<TidalModelMapper>();
            StreamingQuality desiredQuality = releaseQuality.HasValue
                ? mapper.ToStreamingQuality(releaseQuality.Value)
                : mapper.ToStreamingQuality(Settings.PreferredQuality);

            // Capture orchestrator reference to avoid closure over `this`
            SimpleDownloadOrchestrator orchestrator = this._orchestrator;

            // Generate unique download ID
            string downloadId = Guid.NewGuid().ToString("N");
            string outputPath = BuildOutputPath(remoteAlbum);

            // Create download item for tracking via Common primitive.
            HostBridgeDownloadItem downloadItem = new()
            {
                DownloadId = downloadId,
                AlbumId = albumId,
                Title = albumTitle,
                Artist = artistName,
                OutputPath = outputPath,
                StartedAt = DateTime.UtcNow
            };
            downloadItem.SetStatus(HostBridgeDownloadItemStatus.Downloading);
            downloadItem.SetProgress(0);

            ActiveDownloads.Add(downloadItem);

            // Start actual download using the orchestrator
            _ = Task.Run(async () =>
            {
                try
                {
                    this._logger.Debug("Starting async download for album {0}", albumId);

                    // Create progress reporter to update download item
                    Progress<DownloadProgress> progressReporter = new(p =>
                    {
                        if (ActiveDownloads.TryGet(downloadId, out HostBridgeDownloadItem? item) && item is not null)
                        {
                            item.SetProgress(p.PercentComplete);
                        }
                    });

                    DownloadResult result = await orchestrator.DownloadAlbumAsync(
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
                    this._logger.Error(ex, "Failed to download album {0}", albumId);
                    if (ActiveDownloads.TryGet(downloadId, out HostBridgeDownloadItem? item) && item is not null)
                    {
                        item.SetStatus(HostBridgeDownloadItemStatus.Failed);
                        item.CompletedAt = DateTime.UtcNow;
                    }
                }
            });

            this._logger.Debug("Tidal download started with ID: {0}", downloadId);
            return Task.FromResult(downloadId);
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

            // Initialize services and test authentication
            EnsureServicesInitialized();

            IStreamingAuthManager? authManager = this._serviceProvider.GetService<IStreamingAuthManager>();
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
            this._logger.Error(ex, "Tidalarr download client test failed");
            failures.Add(new ValidationFailure("Test", $"Test failed: {ex.Message}"));
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
        string artistName = FileSystemUtilities.SanitizeFileName(remoteAlbum.Artist?.Name ?? "Unknown Artist");
        string albumTitle = FileSystemUtilities.SanitizeFileName(remoteAlbum.Albums?.FirstOrDefault()?.Title ?? "Unknown Album");

        return Path.Combine(basePath, artistName, albumTitle);
    }

    public void Dispose()
    {
        if (this._serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
            this._logger.Debug("Disposed Tidal download service provider");
        }
    }
}

// TidalDownloadItem removed — replaced by Lidarr.Plugin.Common.HostBridge.HostBridgeDownloadItem
// (Wave A item 1 of the May 2026 unification plan).
