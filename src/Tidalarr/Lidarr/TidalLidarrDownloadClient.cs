using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentValidation.Results;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Utilities;
using Microsoft.Extensions.DependencyInjection;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr;

public class TidalLidarrDownloadClient : DownloadClientBase<TidalDownloadSettings>
{
    public TidalLidarrDownloadClient(IConfigService configService,
                                     IDiskProvider diskProvider,
                                     IRemotePathMappingService remotePathMappingService,
                                     ILocalizationService localizationService,
                                     NLog.Logger logger)
        : base(configService, diskProvider, remotePathMappingService, localizationService, logger)
    {
    }

    public override string Name => "Tidal";

    public override string Protocol => nameof(TidalProtocol);

    public override async Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
    {
        if (remoteAlbum?.Release?.DownloadUrl is null)
        {
            throw new DownloadClientException("Release download URL is missing.");
        }

        var (entityType, entityId) = TidalProtocol.ParseUrl(remoteAlbum.Release.DownloadUrl);
        if (!string.Equals(entityType, "album", StringComparison.OrdinalIgnoreCase))
        {
            throw new DownloadClientException($"Unsupported Tidal link type '{entityType}'. Only album downloads are supported.");
        }

        if (string.IsNullOrWhiteSpace(Settings.DownloadPath))
        {
            throw new DownloadClientException("Download path is not configured.");
        }

        Directory.CreateDirectory(Settings.DownloadPath);

        var artistName = remoteAlbum.Artist?.Name ?? remoteAlbum.Release.Artist ?? "Unknown Artist";
        var albumTitle = remoteAlbum.Release.Album ?? remoteAlbum.Release.Title ?? "Unknown Album";

        var artistFolder = FileNameSanitizer.SanitizeFileName(artistName);
        var albumFolder = FileNameSanitizer.SanitizeFileName(albumTitle);
        var destination = Path.Combine(Settings.DownloadPath, artistFolder, albumFolder);
        Directory.CreateDirectory(destination);

        using var provider = BuildServiceProvider();
        var orchestrator = TidalModule.CreateOrchestrator(provider);
        var preferredQuality = MapPreferredQuality(Settings.PreferredQuality);

        var result = await orchestrator.DownloadAlbumAsync(entityId, destination, preferredQuality).ConfigureAwait(false);

        if (!result.Success)
        {
            var message = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Tidal download failed." : result.ErrorMessage;
            throw new DownloadClientException(message);
        }

        return Guid.NewGuid().ToString("N");
    }

    public override IEnumerable<DownloadClientItem> GetItems()
    {
        yield break;
    }

    public override void RemoveItem(DownloadClientItem item, bool deleteData)
    {
        if (deleteData)
        {
            DeleteItemData(item);
        }
    }

    public override DownloadClientInfo GetStatus()
    {
        return new DownloadClientInfo
        {
            IsLocalhost = true,
            OutputRootFolders = new List<OsPath>
            {
                new OsPath(Settings.DownloadPath)
            }
        };
    }

    protected override void Test(List<ValidationFailure> failures)
    {
        var validation = Settings.Validate();
        if (!validation.IsValid)
        {
            failures.AddRange(validation.Errors);
        }
    }

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Settings);
        TidalModule.RegisterServices(services);
        return services.BuildServiceProvider();
    }

    private static StreamingQuality MapPreferredQuality(TidalQuality quality)
    {
        return quality switch
        {
            TidalQuality.Low => new StreamingQuality { Id = "LOW", Name = "Low", Format = "AAC", Bitrate = 96, SampleRate = 44100 },
            TidalQuality.High => new StreamingQuality { Id = "HIGH", Name = "High", Format = "AAC", Bitrate = 320, SampleRate = 44100 },
            TidalQuality.Lossless => new StreamingQuality { Id = "LOSSLESS", Name = "Lossless", Format = "FLAC", BitDepth = 16, SampleRate = 44100 },
            TidalQuality.HiRes => new StreamingQuality { Id = "HI_RES", Name = "Hi-Res", Format = "FLAC", BitDepth = 24, SampleRate = 96000 },
            _ => new StreamingQuality { Id = "LOSSLESS", Name = "Lossless", Format = "FLAC", BitDepth = 16, SampleRate = 44100 }
        };
    }
}
