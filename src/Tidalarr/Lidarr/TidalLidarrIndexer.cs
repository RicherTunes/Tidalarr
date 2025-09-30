using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using Tidalarr.Integration;
using NzbDrone.Core.Parser;

namespace Tidalarr;

public class TidalLidarrIndexer : IndexerBase<TidalIndexerSettings>
{
    public TidalLidarrIndexer(IIndexerStatusService indexerStatusService,
                              IConfigService configService,
                              IParsingService parsingService,
                              NLog.Logger logger)
        : base(indexerStatusService, configService, parsingService, logger)
    {
    }

    public override string Name => "Tidal";

    public override string Protocol => nameof(TidalProtocol);

    public override bool SupportsRss => false;

    public override bool SupportsSearch => true;

    public override Task<IList<ReleaseInfo>> FetchRecent()
    {
        return Task.FromResult<IList<ReleaseInfo>>(Array.Empty<ReleaseInfo>());
    }

    public override async Task<IList<ReleaseInfo>> Fetch(AlbumSearchCriteria searchCriteria)
    {
        var query = searchCriteria?.CleanAlbumQuery ?? searchCriteria?.AlbumTitle ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ReleaseInfo>();
        }

        var releases = await SearchAsync(query, StreamingSearchType.Album).ConfigureAwait(false);
        return CleanupReleases(releases);
    }

    public override async Task<IList<ReleaseInfo>> Fetch(ArtistSearchCriteria searchCriteria)
    {
        var query = searchCriteria?.CleanArtistQuery ?? searchCriteria?.ArtistQuery ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ReleaseInfo>();
        }

        var releases = await SearchAsync(query, StreamingSearchType.Album).ConfigureAwait(false);
        return CleanupReleases(releases);
    }

    public override HttpRequest GetDownloadRequest(string link)
    {
        return new HttpRequest(link);
    }

    protected override Task Test(List<ValidationFailure> failures)
    {
        var validation = Settings.Validate();
        if (!validation.IsValid)
        {
            failures.AddRange(validation.Errors);
        }

        return Task.CompletedTask;
    }

    private async Task<IList<ReleaseInfo>> SearchAsync(string query, StreamingSearchType resultType)
    {
        using var provider = BuildServiceProvider();
        var indexer = provider.GetRequiredService<TidalIndexer>();

        var searchResults = await indexer.SearchEnhancedAsync(query).ConfigureAwait(false);
        var filtered = FilterByType(searchResults, resultType);

        return filtered
            .Select(MapToReleaseInfo)
            .Where(release => release is not null)
            .Select(release => release!)
            .ToList();
    }

    private static IEnumerable<StreamingSearchResult> FilterByType(IEnumerable<StreamingSearchResult> results, StreamingSearchType type)
    {
        if (results == null)
        {
            return Enumerable.Empty<StreamingSearchResult>();
        }

        return results.Where(result => result.Type == type);
    }

    private ReleaseInfo? MapToReleaseInfo(StreamingSearchResult result)
    {
        if (string.IsNullOrWhiteSpace(result?.Id))
        {
            return null;
        }

        var release = LidarrIntegrationHelpers.CreateReleaseInfo(result,
            Name,
            (guid, title, size, downloadUrl, infoUrl, publishDate, categories) => new ReleaseInfo
            {
                Guid = guid,
                Title = title,
                Size = size,
                DownloadUrl = downloadUrl,
                InfoUrl = infoUrl,
                PublishDate = publishDate,
                Artist = result.Artist ?? string.Empty,
                Album = result.Album ?? result.Title ?? string.Empty,
                DownloadProtocol = Protocol
            },
            Protocol);

        return release;
    }

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Settings);
        TidalModule.RegisterServices(services);
        return services.BuildServiceProvider();
    }
}
