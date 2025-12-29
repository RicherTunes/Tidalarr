using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Interfaces;
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
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Lidarr-native download client extending DownloadClientBase for plugin discovery.
/// Lidarr's plugin system scans for classes extending this base class.
/// Uses TidalModule services internally for actual download functionality.
/// </summary>
public class TidalLidarrDownloadClient : DownloadClientBase<TidalLidarrDownloadClientSettings>
{
    // IMPORTANT: Lidarr may construct plugin types more than once. Download tracking must be
    // process-wide so queue polling always sees active downloads, even if a new instance is created.
    private static readonly ConcurrentDictionary<string, TidalDownloadItem> ActiveDownloads = new();
    private new readonly Logger _logger;
    private IServiceProvider _serviceProvider;
    private bool _servicesInitialized;
    private SimpleDownloadOrchestrator _orchestrator;
    private static readonly TimeSpan CompletedDownloadRetention = TimeSpan.FromMinutes(30);

    public override string Name => "Tidalarr";
    public override string Protocol => nameof(TidalarrDownloadProtocol);

    public TidalLidarrDownloadClient(
        IConfigService configService,
        IDiskProvider diskProvider,
        IRemotePathMappingService remotePathMappingService,
        ILocalizationService localizationService,
        Logger logger)
        : base(configService, diskProvider, remotePathMappingService, localizationService, logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialize Tidal services from TidalModule when first needed.
    /// </summary>
    private void EnsureServicesInitialized()
    {
        if (_servicesInitialized) return;

        try
        {
            var services = new ServiceCollection();

            // Use the ToTidalSettings() method for proper conversion
            var downloadSettings = Settings.ToTidalSettings();
            services.AddSingleton(downloadSettings);

            // Create TidalarrSettings from Lidarr-native settings
            var tidalarrSettings = new TidalarrSettings
            {
                ConfigPath = Settings.ConfigPath,
                RedirectUrl = Settings.RedirectUrl,
                DownloadPath = Settings.DownloadPath,
                PreferredQuality = Settings.PreferredQuality,
                IncludeMqa = Settings.IncludeMqa,
                ExtractFlac = Settings.ExtractFlac,
                DownloadDelay = Settings.DownloadDelay
            };
            services.AddSingleton(tidalarrSettings);

            // Register all Tidal services
            TidalModule.RegisterServices(services);

            _serviceProvider = services.BuildServiceProvider();
            _orchestrator = TidalModule.CreateOrchestrator(_serviceProvider);
            _servicesInitialized = true;
            _logger.Debug("Tidal download services initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize Tidal download services");
            throw;
        }
    }

    public override Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
    {
        try
        {
            EnsureServicesInitialized();

            var albumTitle = remoteAlbum.Albums?.FirstOrDefault()?.Title ?? "Unknown Album";
            var artistName = remoteAlbum.Artist?.Name ?? "Unknown Artist";

            _logger.Info("Starting Tidal download: {0} - {1}", artistName, albumTitle);

            // Extract album ID from release
            var albumId = ExtractAlbumIdFromRelease(remoteAlbum.Release);
            if (string.IsNullOrWhiteSpace(albumId))
            {
                throw new InvalidOperationException("Could not extract album ID from release");
            }

            // Generate unique download ID
            var downloadId = Guid.NewGuid().ToString("N");
            var outputPath = BuildOutputPath(remoteAlbum);

            // Create download item for tracking
            var downloadItem = new TidalDownloadItem
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
                    _logger.Debug("Starting async download for album {0}", albumId);

                    // Create progress reporter to update download item
                    var progressReporter = new Progress<DownloadProgress>(p =>
                    {
                        if (ActiveDownloads.TryGetValue(downloadId, out var item))
                        {
                            item.Progress = p.PercentComplete;
                        }
                    });

                    var result = await _orchestrator.DownloadAlbumAsync(
                        albumId,
                        outputPath,
                        quality: null,
                        progress: progressReporter);

                    // Mark as completed
                    if (ActiveDownloads.TryGetValue(downloadId, out var item))
                    {
                        item.Status = result.Success ? DownloadItemStatus.Completed : DownloadItemStatus.Failed;
                        item.Progress = 100;
                        item.CompletedAt = DateTime.UtcNow;
                        _logger.Info("Completed download: {0} - {1} ({2} files)", artistName, albumTitle, result.FilePaths?.Count ?? 0);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to download album {0}", albumId);
                    if (ActiveDownloads.TryGetValue(downloadId, out var item))
                    {
                        item.Status = DownloadItemStatus.Failed;
                        item.CompletedAt = DateTime.UtcNow;
                    }
                }
            });

            _logger.Debug("Tidal download started with ID: {0}", downloadId);
            return Task.FromResult(downloadId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start Tidal download");
            throw;
        }
    }

    public override IEnumerable<DownloadClientItem> GetItems()
    {
        var result = new List<DownloadClientItem>();

        // Best-effort cleanup to prevent unbounded growth if Lidarr doesn't call RemoveItem.
        var now = DateTime.UtcNow;
        foreach (var kv in ActiveDownloads)
        {
            var item = kv.Value;

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
        if (ActiveDownloads.TryRemove(item.DownloadId, out var download))
        {
            _logger.Debug("Removed Tidal download: {0}", item.DownloadId);

            if (deleteData && Directory.Exists(download.OutputPath))
            {
                try
                {
                    Directory.Delete(download.OutputPath, recursive: true);
                    _logger.Debug("Deleted download data at: {0}", download.OutputPath);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to delete download data at: {0}", download.OutputPath);
                }
            }
        }
    }

    public override DownloadClientInfo GetStatus()
    {
        return new DownloadClientInfo
        {
            IsLocalhost = true,
            OutputRootFolders = new List<OsPath> { new OsPath(Settings.DownloadPath) }
        };
    }

    protected override void Test(List<ValidationFailure> failures)
    {
        try
        {
            _logger.Info("Testing Tidalarr download client connection...");

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
                    Directory.CreateDirectory(Settings.DownloadPath);
                    _logger.Debug("Created download directory: {0}", Settings.DownloadPath);
                }
                catch (Exception ex)
                {
                    failures.Add(new ValidationFailure("DownloadPath", $"Cannot create download path: {ex.Message}"));
                    return;
                }
            }

            // Initialize services and test authentication
            EnsureServicesInitialized();

            var authManager = _serviceProvider.GetService<IStreamingAuthManager>();
            if (authManager != null)
            {
                try
                {
                    authManager.EnsureValidSessionAsync().GetAwaiter().GetResult();
                    _logger.Debug("Tidal authentication session is valid");
                }
                catch (Exception authEx)
                {
                    _logger.Warn(authEx, "Tidal authentication not configured or invalid");
                    failures.Add(new ValidationFailure("Authentication",
                        "Not authenticated with Tidal. Please complete the OAuth flow using the redirect URL."));
                    return;
                }
            }

            _logger.Info("Tidalarr download client test completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Tidalarr download client test failed");
            failures.Add(new ValidationFailure("Test", $"Test failed: {ex.Message}"));
        }
    }

    private string ExtractAlbumIdFromRelease(ReleaseInfo release)
    {
        // Try to extract album ID from release GUID or Info URL
        if (!string.IsNullOrWhiteSpace(release?.Guid))
        {
            // Format: tidal:album:12345678
            var parts = release.Guid.Split(':');
            if (parts.Length >= 3 && parts[0].Equals("tidal", StringComparison.OrdinalIgnoreCase))
            {
                return parts[2];
            }
            return release.Guid;
        }

        if (!string.IsNullOrWhiteSpace(release?.InfoUrl))
        {
            // Try to extract from URL: https://tidal.com/browse/album/12345678
            var uri = new Uri(release.InfoUrl);
            var segments = uri.AbsolutePath.Split('/');
            var albumIndex = Array.IndexOf(segments, "album");
            if (albumIndex >= 0 && albumIndex < segments.Length - 1)
            {
                return segments[albumIndex + 1];
            }
        }

        return null;
    }

    private string BuildOutputPath(RemoteAlbum remoteAlbum)
    {
        var basePath = Settings.DownloadPath;
        var artistName = SanitizeFileName(remoteAlbum.Artist?.Name ?? "Unknown Artist");
        var albumTitle = SanitizeFileName(remoteAlbum.Albums?.FirstOrDefault()?.Title ?? "Unknown Album");

        return Path.Combine(basePath, artistName, albumTitle);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "Unknown";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = fileName;

        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        return sanitized.Trim();
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
