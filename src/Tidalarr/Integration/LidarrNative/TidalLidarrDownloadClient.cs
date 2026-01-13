using System.Collections.Concurrent;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Security;
using Lidarr.Plugin.Common.Services.Authentication;
using Lidarr.Plugin.Common.Services.Download;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

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
    Logger logger) : DownloadClientBase<TidalLidarrDownloadClientSettings>(configService, diskProvider, remotePathMappingService, localizationService, logger)
{
    // IMPORTANT: Lidarr may construct plugin types more than once. Download tracking must be
    // process-wide so queue polling always sees active downloads, even if a new instance is created.
    private static readonly ConcurrentDictionary<string, TidalDownloadItem> ActiveDownloads = new();
    private new readonly Logger _logger = logger;
    private IServiceProvider _serviceProvider;
    private bool _servicesInitialized;
    private SimpleDownloadOrchestrator _orchestrator;
    private static readonly TimeSpan CompletedDownloadRetention = TimeSpan.FromMinutes(30);

    public override string Name => "Tidalarr";
    public override string Protocol => nameof(TidalarrDownloadProtocol);

    /// <summary>
    /// Initialize Tidal services from TidalModule when first needed.
    /// </summary>
    private void EnsureServicesInitialized()
    {
        if (this._servicesInitialized) return;

        try
        {
            ServiceCollection services = new();

            // Use the ToTidalSettings() method for proper conversion
            TidalDownloadClientSettings downloadSettings = Settings.ToTidalSettings();
            _ = services.AddSingleton(downloadSettings);

            // Create TidalarrSettings from Lidarr-native settings
            TidalarrSettings tidalarrSettings = new()
            {
                ConfigPath = Settings.ConfigPath,
                RedirectUrl = Settings.RedirectUrl,
                DownloadPath = Settings.DownloadPath,
                PreferredQuality = Settings.PreferredQuality,
                IncludeMqa = Settings.IncludeMqa,
                ExtractFlac = Settings.ExtractFlac,
                DownloadDelay = Settings.DownloadDelay
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

    public override Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
    {
        try
        {
            EnsureServicesInitialized();

            string albumTitle = remoteAlbum.Albums?.FirstOrDefault()?.Title ?? "Unknown Album";
            string artistName = remoteAlbum.Artist?.Name ?? "Unknown Artist";

            this._logger.Info("Starting Tidal download: {0} - {1}", artistName, albumTitle);

            // Extract album ID from release
            string albumId = ExtractAlbumIdFromRelease(remoteAlbum.Release);
            if (string.IsNullOrWhiteSpace(albumId))
            {
                throw new InvalidOperationException("Could not extract album ID from release");
            }

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
                            item.Progress = p.PercentComplete;
                        }
                    });

                    DownloadResult result = await this._orchestrator.DownloadAlbumAsync(
                        albumId,
                        outputPath,
                        quality: null,
                        progress: progressReporter);

                    // Mark as completed
                    if (ActiveDownloads.TryGetValue(downloadId, out TidalDownloadItem? item))
                    {
                        item.Status = result.Success ? DownloadItemStatus.Completed : DownloadItemStatus.Failed;
                        item.Progress = 100;
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
                        item.Status = DownloadItemStatus.Failed;
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

            if ((item.Status == DownloadItemStatus.Completed || item.Status == DownloadItemStatus.Failed) &&
                item.CompletedAt.HasValue &&
                now - item.CompletedAt.Value > CompletedDownloadRetention)
            {
                _ = ActiveDownloads.TryRemove(kv.Key, out _);
                continue;
            }

            result.Add(new DownloadClientItem
            {
                DownloadId = item.DownloadId,
                Title = $"{item.Artist} - {item.Title}",
                Status = item.Status,
                TotalSize = item.TotalSize,
                RemainingSize = item.TotalSize - (long)(item.TotalSize * item.Progress / 100),
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
    /// Extracts album ID from GUID, handling both prefixed (e.g., "2_tidal:album:12345678")
    /// and unprefixed (e.g., "tidal:album:12345678") formats.
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

        // Format: tidal:album:12345678
        string[] parts = normalizedGuid.Split(':');
        return parts.Length >= 3 && parts[0].Equals("tidal", StringComparison.OrdinalIgnoreCase) ? parts[2] : null;
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
        string artistName = SanitizeFileName(remoteAlbum.Artist?.Name ?? "Unknown Artist");
        string albumTitle = SanitizeFileName(remoteAlbum.Albums?.FirstOrDefault()?.Title ?? "Unknown Album");

        return Path.Combine(basePath, artistName, albumTitle);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "Unknown";
        string sanitized = Sanitize.PathSegment(fileName);
        return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
    }
}

/// <summary>
/// Internal download item tracking for Tidal downloads.
/// </summary>
internal class TidalDownloadItem
{
    public string DownloadId { get; set; } = string.Empty;
    public string AlbumId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public DownloadItemStatus Status { get; set; } = DownloadItemStatus.Queued;
    public double Progress { get; set; }
    public long TotalSize { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string OutputPath { get; set; } = string.Empty;
}
