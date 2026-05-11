using System.Collections.Concurrent;
using FluentValidation.Results;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
using Lidarr.Plugin.Common.Services.Diagnostics;
using Lidarr.Plugin.Common.Services.Download;
using Lidarr.Plugin.Common.Utilities;
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
    private static readonly ConcurrentDictionary<string, TidalDownloadItem> ActiveDownloads = new();
    private new readonly Logger _logger = logger;
    private IServiceProvider _serviceProvider;
    private bool _servicesInitialized;
    private readonly object _initLock = new();
    private SimpleDownloadOrchestrator _orchestrator;
    private static readonly TimeSpan CompletedDownloadRetention = TimeSpan.FromMinutes(30);

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

            // Create download item for tracking
            TidalDownloadItem downloadItem = new()
            {
                DownloadId = downloadId,
                AlbumId = albumId,
                Title = albumTitle,
                Artist = artistName,
                Status = DownloadItemStatus.Downloading,
                Progress = 0,
                StartedAt = DateTime.UtcNow,
                OutputPath = outputPath
            };

            ActiveDownloads[downloadId] = downloadItem;

            // Start actual download using the orchestrator
            _ = Task.Run(async () =>
            {
                try
                {
                    this._logger.Debug("Starting async download for album {0}", albumId);

                    // Create progress reporter to update download item
                    Progress<DownloadProgress> progressReporter = new(p =>
                    {
                        if (ActiveDownloads.TryGetValue(downloadId, out TidalDownloadItem? item))
                        {
                            item.UpdateProgress(p.PercentComplete);
                        }
                    });

                    DownloadResult result = await orchestrator.DownloadAlbumAsync(
                        albumId,
                        outputPath,
                        quality: desiredQuality,
                        progress: progressReporter);

                    // Mark as completed (thread-safe updates)
                    if (ActiveDownloads.TryGetValue(downloadId, out TidalDownloadItem? item))
                    {
                        item.UpdateStatus(result.Success ? DownloadItemStatus.Completed : DownloadItemStatus.Failed);
                        item.UpdateProgress(100);
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
                    if (ActiveDownloads.TryGetValue(downloadId, out TidalDownloadItem? item))
                    {
                        item.UpdateStatus(DownloadItemStatus.Failed);
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

        // Best-effort cleanup to prevent unbounded growth if Lidarr doesn't call RemoveItem.
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, TidalDownloadItem> kv in ActiveDownloads)
        {
            TidalDownloadItem item = kv.Value;
            DownloadItemStatus status = item.GetStatus();
            double progress = item.GetProgress();

            if ((status == DownloadItemStatus.Completed || status == DownloadItemStatus.Failed) &&
                item.CompletedAt.HasValue &&
                now - item.CompletedAt.Value > CompletedDownloadRetention)
            {
                if (ActiveDownloads.TryRemove(kv.Key, out _))
                {
                    this._logger.Debug("Evicted stale download {0} ({1}) after retention period", kv.Key, status);
                }

                continue;
            }

            result.Add(new DownloadClientItem
            {
                DownloadId = item.DownloadId,
                Title = $"{item.Artist} - {item.Title}",
                Status = status,
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
        if (ActiveDownloads.TryRemove(item.DownloadId, out TidalDownloadItem? download))
        {
            this._logger.Debug("Removed Tidal download: {0}", item.DownloadId);

            if (deleteData && Directory.Exists(download.OutputPath))
            {
                try
                {
                    Directory.Delete(download.OutputPath, recursive: true);
                    this._logger.Debug("Deleted download data at: {0}", download.OutputPath);
                }
                catch (Exception ex)
                {
                    this._logger.Warn(ex, "Failed to delete download data at: {0}", download.OutputPath);
                }
            }
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

            // Basic settings validation
            if (string.IsNullOrWhiteSpace(Settings.ConfigPath))
            {
                failures.Add(new ValidationFailure("ConfigPath", "Config path is required"));
                return;
            }

            if (string.IsNullOrWhiteSpace(Settings.DownloadPath))
            {
                failures.Add(new ValidationFailure("DownloadPath", "Download path is required"));
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
            // Use common's HttpExceptionClassifier so the user-visible message is
            // an actionable category hint (auth / rate-limit / network / timeout
            // / server / etc.) rather than a leaked CLR type name. The operator
            // still gets the full stack trace via _logger.Error above.
            failures.Add(new ValidationFailure("Test", BuildTestFailureMessage(ex)));
        }
    }

    /// <summary>
    /// Build the user-visible <c>Test()</c> failure text from an exception caught
    /// during download-client validation. Delegates to common's
    /// <see cref="HttpExceptionClassifier"/> for a categorised hint and appends
    /// a pointer to the Lidarr log for operator deep-dives.
    ///
    /// CLR type names are deliberately stripped — they are not actionable for
    /// end users. Operators get the full stack trace via the logger call at
    /// the catch site.
    /// </summary>
    public static string BuildTestFailureMessage(Exception ex)
    {
        var classification = HttpExceptionClassifier.Classify(ex);
        return $"{classification.Hint} Full details in Lidarr logs.";
    }

    private string ExtractAlbumIdFromRelease(ReleaseInfo release)
    {
        // Try GUID first
        string? albumId = ExtractAlbumIdFromGuid(release?.Guid);
        if (!string.IsNullOrWhiteSpace(albumId))
        {
            return albumId;
        }

        // Fall back to InfoUrl
        return ExtractAlbumIdFromInfoUrl(release?.InfoUrl) ?? release?.Guid ?? string.Empty;
    }

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

    /// <summary>
    /// Extracts album ID from GUID, handling prefixed ("2_tidal:album:12345678"),
    /// unprefixed ("tidal:album:12345678"), and quality-suffixed ("tidal:album:12345678:Lossless") formats.
    /// </summary>
    internal static string? ExtractAlbumIdFromGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return null;
        }

        string normalizedGuid = guid;

        // Strip indexer ID prefix if present (format: "2_tidal:album:12345678")
        int prefixEnd = guid.IndexOf("_tidal:", StringComparison.OrdinalIgnoreCase);
        if (prefixEnd >= 0)
        {
            normalizedGuid = guid[(prefixEnd + 1)..]; // Remove "2_" prefix, keep "tidal:album:12345678"
        }

        // Format: tidal:album:12345678 (reject empty/whitespace ID segments)
        string[] parts = normalizedGuid.Split(':');
        if (parts.Length >= 3 && parts[0].Equals("tidal", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(parts[2]))
        {
            return parts[2];
        }

        return null;
    }

    private static string? ExtractAlbumIdFromInfoUrl(string? infoUrl)
    {
        if (string.IsNullOrWhiteSpace(infoUrl))
        {
            return null;
        }

        try
        {
            // Try to extract from URL: https://tidal.com/browse/album/12345678
            Uri uri = new(infoUrl);
            string[] segments = uri.AbsolutePath.Split('/');
            int albumIndex = Array.IndexOf(segments, "album");
            if (albumIndex >= 0 && albumIndex < segments.Length - 1)
            {
                return segments[albumIndex + 1];
            }
        }
        catch
        {
            // Invalid URI format
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

/// <summary>
/// Internal download item tracking for Tidal downloads.
/// Thread-safe for concurrent reads (GetItems polling) and writes (progress callbacks).
/// </summary>
internal class TidalDownloadItem
{
    private volatile int _status = (int)DownloadItemStatus.Queued;
    private long _progressBits; // stored as long bit pattern for atomic read/write of double

    public string DownloadId { get; set; } = string.Empty;
    public string AlbumId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;

    public DownloadItemStatus Status
    {
        get => (DownloadItemStatus)Volatile.Read(ref this._status);
        set => Volatile.Write(ref this._status, (int)value);
    }

    public double Progress
    {
        get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref this._progressBits));
        set => Interlocked.Exchange(ref this._progressBits, BitConverter.DoubleToInt64Bits(value));
    }

    public long TotalSize { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Thread-safe progress update.</summary>
    public void UpdateProgress(double value) => Progress = value;

    /// <summary>Thread-safe status update.</summary>
    public void UpdateStatus(DownloadItemStatus value) => Status = value;

    /// <summary>Thread-safe progress read.</summary>
    public double GetProgress() => Progress;

    /// <summary>Thread-safe status read.</summary>
    public DownloadItemStatus GetStatus() => Status;
}
