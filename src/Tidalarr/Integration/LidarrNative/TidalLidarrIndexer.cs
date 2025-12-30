using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Lidarr-native indexer extending HttpIndexerBase for plugin discovery.
/// Uses TidalModule services internally for actual search functionality.
/// </summary>
public class TidalLidarrIndexer : HttpIndexerBase<TidalLidarrIndexerSettings>   
{
    public override string Name => "Tidalarr";
    public override string Protocol => nameof(TidalarrDownloadProtocol);
    public override bool SupportsRss => false;
    public override bool SupportsSearch => true;
    public override int PageSize => 100;

    private new readonly Logger _logger;
    private IServiceProvider _serviceProvider;
    private bool _servicesInitialized;

    public TidalLidarrIndexer(
        IHttpClient httpClient,
        IIndexerStatusService indexerStatusService,
        IConfigService configService,
        IParsingService parsingService,
        Logger logger)
        : base(httpClient, indexerStatusService, configService, parsingService, logger)
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

            // Register settings from Lidarr configuration
            var indexerSettings = new TidalIndexerSettings
            {
                ConfigPath = Settings.ConfigPath,
                RedirectUrl = Settings.RedirectUrl,
                TidalMarket = Settings.TidalMarket,
                EarlyReleaseLimit = Settings.EarlyReleaseLimit,
                EnableCache = Settings.EnableCache,
                CacheDuration = Settings.CacheDuration
            };
            services.AddSingleton(indexerSettings);
            services.AddSingleton(new TidalarrSettings
            {
                ConfigPath = Settings.ConfigPath,
                RedirectUrl = Settings.RedirectUrl,
                TidalMarket = Settings.TidalMarket
            });

            // Register all Tidal services
            TidalModule.RegisterServices(services);

            _serviceProvider = services.BuildServiceProvider();
            _servicesInitialized = true;
            _logger.Debug("Tidal services initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize Tidal services");
            throw;
        }
    }

    public override IIndexerRequestGenerator GetRequestGenerator()
    {
        // We use a special request generator that encodes the search query
        // The actual search happens in the parser using our services
        return new TidalLidarrRequestGenerator(Settings, _logger);
    }

    public override IParseIndexerResponse GetParser()
    {
        EnsureServicesInitialized();
        return new TidalLidarrParser(Settings, _serviceProvider, _logger);      
    }

    protected override async Task<IList<ReleaseInfo>> FetchReleases(
        Func<IIndexerRequestGenerator, IndexerPageableRequestChain> pageableRequestChainSelector,
        bool isRecent = false)
    {
        EnsureServicesInitialized();

        var releases = new List<ReleaseInfo>();

        // Ensure valid session early so Lidarr reports a clear authentication error.
        var authManager = _serviceProvider.GetService<IStreamingAuthManager>();
        if (authManager != null)
        {
            await authManager.EnsureValidSessionAsync().ConfigureAwait(false);
        }

        var searchService = _serviceProvider.GetService<TidalSearchService>();
        if (searchService == null)
        {
            _logger.Error("TidalSearchService not available");
            return releases;
        }

        var requestGenerator = GetRequestGenerator();
        var requestChain = pageableRequestChainSelector(requestGenerator);

        foreach (var tier in requestChain.GetAllTiers())
        {
            foreach (var request in tier)
            {
                var requestUrl = request.HttpRequest?.Url?.ToString() ?? string.Empty;
                if (!TryExtractSearchQuery(requestUrl, out var query))
                {
                    _logger.Warn("Unexpected request URL format: {0}", requestUrl);
                    continue;
                }

                try
                {
                    var searchResults = await searchService
                        .SearchWithQualityDetectionAsync(query, TidalQuality.Lossless)
                        .ConfigureAwait(false);

                    if (searchResults.Albums == null || searchResults.Albums.Count == 0)
                    {
                        continue;
                    }

                    foreach (var album in searchResults.Albums)
                    {
                        var release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);
                        if (release != null)
                        {
                            releases.Add(release);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Tidal search failed for query: {0}", query);
                }
            }
        }

        // Ensure Lidarr can attribute grabs to this indexer.
        var indexerId = Definition?.Id ?? 0;
        var indexerName = Definition?.Name;

        foreach (var release in releases)
        {
            release.IndexerId = indexerId;
            if (string.IsNullOrWhiteSpace(release.Indexer) && !string.IsNullOrWhiteSpace(indexerName))
            {
                release.Indexer = indexerName;
            }
        }

        // Deduplicate by Guid (same query can appear in multiple tiers).
        return releases
            .Where(r => !string.IsNullOrWhiteSpace(r.Guid))
            .GroupBy(r => r.Guid)
            .Select(g => g.First())
            .ToList();
    }

    private static bool TryExtractSearchQuery(string requestUrl, out string query)
    {
        query = string.Empty;

        if (!requestUrl.StartsWith("tidal://search", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var q = queryParams["query"];
        if (string.IsNullOrWhiteSpace(q))
        {
            return false;
        }

        query = q;
        return true;
    }

    protected override async Task Test(List<ValidationFailure> failures)
    {
        try
        {
            _logger.Info("Testing Tidalarr indexer connection...");

            // Basic settings validation
            if (string.IsNullOrWhiteSpace(Settings.ConfigPath))
            {
                failures.Add(new ValidationFailure("ConfigPath", "Config path is required"));
                return;
            }

            // Ensure config directory exists
            if (!Directory.Exists(Settings.ConfigPath))
            {
                try
                {
                    Directory.CreateDirectory(Settings.ConfigPath);
                    _logger.Info($"Created config directory: {Settings.ConfigPath}");
                }
                catch (Exception ex)
                {
                    failures.Add(new ValidationFailure("ConfigPath", $"Failed to create config directory: {ex.Message}"));
                    return;
                }
            }

            // Initialize services
            EnsureServicesInitialized();

            // Check if we have a valid redirect URL with authorization code
            bool hasRedirectUrl = !string.IsNullOrWhiteSpace(Settings.RedirectUrl);

            // Try to get existing valid session first
            var authManager = _serviceProvider.GetService<IStreamingAuthManager>();
            var authService = _serviceProvider.GetService<ITidalAuth>();

            if (authService != null)
            {
                try
                {
                    // Try to get valid tokens
                    await authService.GetValidTokensAsync();
                    _logger.Debug("Tidal authentication session is valid");
                }
                catch (InvalidOperationException authEx)
                {
                    _logger.Debug(authEx, "No valid Tidal session - checking if we can authenticate...");

                    // No valid tokens - try to exchange redirect URL if provided
                    if (hasRedirectUrl)
                    {
                        var exchangeResult = await TryExchangeAuthorizationCode(authService, failures);
                        if (!exchangeResult)
                        {
                            return; // Error already added to failures
                        }
                    }
                    else
                    {
                        // No redirect URL - generate auth URL for user
                        await GenerateOAuthAuthUrl(authService, failures);
                        return;
                    }
                }
            }

            // Test a simple search
            var searchService = _serviceProvider.GetService<TidalSearchService>();
            if (searchService != null)
            {
                var testResults = await searchService.SearchWithQualityDetectionAsync("test", TidalQuality.Lossless);
                _logger.Info($"Test search completed. Found {testResults.Albums?.Count ?? 0} albums.");
            }

            _logger.Info("Tidalarr indexer test completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Tidalarr indexer test failed");
            failures.Add(new ValidationFailure("Test", $"Test failed: {ex.Message}"));
        }
    }

    private async Task<bool> TryExchangeAuthorizationCode(ITidalAuth authService, List<ValidationFailure> failures)
    {
        try
        {
            // Parse the redirect URL to extract authorization code
            var callbackResult = authService.ParseCallbackUrl(Settings.RedirectUrl);
            if (!callbackResult.IsSuccess)
            {
                _logger.Warn($"Failed to parse redirect URL: {callbackResult.ErrorMessage}");
                failures.Add(new ValidationFailure("RedirectUrl", callbackResult.ErrorMessage ?? "Invalid redirect URL"));
                return false;
            }

            // Load PKCE state from disk
            var pkceStore = new PKCEStateStore(Settings.ConfigPath);
            var pkceState = await pkceStore.LoadStateAsync();

            if (pkceState == null)
            {
                _logger.Warn("No PKCE state found - auth URL may have expired. Generate a new one.");
                await GenerateOAuthAuthUrl(authService, failures);
                failures.Add(new ValidationFailure("OAuthAuthUrl",
                    "Authorization URL expired. A new one has been generated. Copy it, authenticate, then paste the redirect URL."));
                return false;
            }

            // Validate state matches
            if (pkceState.State != callbackResult.State)
            {
                _logger.Warn("PKCE state mismatch - possible CSRF attack or stale auth URL");
                await GenerateOAuthAuthUrl(authService, failures);
                failures.Add(new ValidationFailure("OAuthAuthUrl",
                    "Authorization state mismatch. A new auth URL has been generated. Please re-authenticate."));
                return false;
            }

            // Exchange authorization code for tokens
            _logger.Info("Exchanging authorization code for tokens...");
            var tokens = await authService.ExchangeCodeAsync(callbackResult.AuthCode, pkceState.CodeVerifier);

            if (tokens == null || string.IsNullOrWhiteSpace(tokens.AccessToken))
            {
                failures.Add(new ValidationFailure("Authentication", "Token exchange failed - received invalid tokens"));
                return false;
            }

            // Clean up PKCE state after successful exchange
            await pkceStore.DeleteStateAsync();

            _logger.Info("Successfully authenticated with Tidal!");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to exchange authorization code");
            failures.Add(new ValidationFailure("Authentication", $"Token exchange failed: {ex.Message}"));
            return false;
        }
    }

    private async Task GenerateOAuthAuthUrl(ITidalAuth authService, List<ValidationFailure> failures)
    {
        try
        {
            // Generate new auth URL + PKCE state
            var authUrlData = await authService.GenerateAuthUrlAsync();

            // Persist PKCE state for later token exchange
            var pkceStore = new PKCEStateStore(Settings.ConfigPath);
            var pkceState = new PKCEState(
                authUrlData.AuthorizationUrl,
                authUrlData.CodeVerifier,
                authUrlData.State,
                authUrlData.ClientUniqueKey,
                DateTime.UtcNow);
            await pkceStore.SaveStateAsync(pkceState);

            _logger.Info($"Generated OAuth authorization URL. Copy it from the 'OAuth Authorization URL' field.");

            // Update settings with the auth URL (this won't persist, but shows in validation message)
            Settings.OAuthAuthUrl = authUrlData.AuthorizationUrl;

            failures.Add(new ValidationFailure("OAuthAuthUrl",
                $"Not authenticated. 1) Copy the OAuth Authorization URL. 2) Open it in a browser and log in. 3) Copy the redirect URL. 4) Paste it in 'OAuth Redirect URL' field. Auth URL: {authUrlData.AuthorizationUrl}"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to generate OAuth authorization URL");
            failures.Add(new ValidationFailure("Authentication", $"Failed to generate auth URL: {ex.Message}"));
        }
    }
}

/// <summary>
/// Request generator for Tidal API searches.
/// Generates placeholder requests - actual search happens in parser.
/// </summary>
public class TidalLidarrRequestGenerator : IIndexerRequestGenerator
{
    private readonly TidalLidarrIndexerSettings _settings;
    private readonly Logger _logger;

    public TidalLidarrRequestGenerator(TidalLidarrIndexerSettings settings, Logger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public IndexerPageableRequestChain GetRecentRequests()
    {
        var chain = new IndexerPageableRequestChain();
        // Tidal doesn't have a traditional RSS feed
        return chain;
    }

    public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
    {
        var chain = new IndexerPageableRequestChain();

        var searchTerm = $"{searchCriteria.ArtistQuery} {searchCriteria.AlbumQuery}".Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            chain.Add(GetSearchRequests(searchTerm));
        }

        return chain;
    }

    public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria)
    {
        var chain = new IndexerPageableRequestChain();

        if (!string.IsNullOrWhiteSpace(searchCriteria.ArtistQuery))
        {
            chain.Add(GetSearchRequests(searchCriteria.ArtistQuery));
        }

        return chain;
    }

    private IEnumerable<IndexerRequest> GetSearchRequests(string searchTerm)
    {
        _logger.Debug($"Generating Tidal search request for: {searchTerm}");

        // Create a placeholder URL that encodes the search query
        // The actual search is performed by the parser using TidalSearchService
        var encodedQuery = Uri.EscapeDataString(searchTerm);
        var requestUrl = $"tidal://search?query={encodedQuery}";

        var request = new HttpRequest(requestUrl);
        request.Headers.Accept = "application/json";

        yield return new IndexerRequest(request);
    }
}

/// <summary>
/// Parser for Tidal search results.
/// Uses TidalSearchService to perform actual searches and converts results to Lidarr format.
/// </summary>
public class TidalLidarrParser : IParseIndexerResponse
{
    private readonly TidalLidarrIndexerSettings _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly Logger _logger;

    public TidalLidarrParser(TidalLidarrIndexerSettings settings, IServiceProvider serviceProvider, Logger logger)
    {
        _settings = settings;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
    {
        var releases = new List<ReleaseInfo>();

        try
        {
            // Extract search query from the placeholder URL
            var requestUrl = indexerResponse.Request?.Url?.ToString() ?? "";
            if (!requestUrl.StartsWith("tidal://search"))
            {
                _logger.Warn("Unexpected request URL format: {0}", requestUrl);
                return releases;
            }

            var uri = new Uri(requestUrl);
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var searchQuery = queryParams["query"];

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                _logger.Warn("No search query found in request");
                return releases;
            }

            // Perform actual search using TidalSearchService
            var searchService = _serviceProvider.GetService<TidalSearchService>();
            if (searchService == null)
            {
                _logger.Error("TidalSearchService not available");
                return releases;
            }

            // Execute search synchronously (we're in a sync context)
            var searchTask = searchService.SearchWithQualityDetectionAsync(searchQuery, TidalQuality.Lossless);
            var searchResults = searchTask.GetAwaiter().GetResult();

            if (searchResults.Albums == null || searchResults.Albums.Count == 0)
            {
                _logger.Debug("No albums found for query: {0}", searchQuery);
                return releases;
            }

            // Convert Tidal albums to Lidarr ReleaseInfo
            foreach (var album in searchResults.Albums)
            {
                try
                {
                    var release = ConvertToReleaseInfoStatic(album);
                    if (release != null)
                    {
                        releases.Add(release);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to convert album: {0}", album.Title);
                }
            }

            _logger.Debug("Parsed {0} releases from Tidal search", releases.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to parse Tidal search response");
        }

        return releases;
    }

    internal static ReleaseInfo ConvertToReleaseInfoStatic(TidalAlbumInfo album)
    {
        if (album == null) return null;

        var artistName = album.Artists?.FirstOrDefault() ?? "Unknown Artist";
        var albumTitle = album.Title ?? "Unknown Album";
        var releaseDate = album.ReleaseDate;

        // Determine quality from available qualities
        var bestQuality = album.AvailableQualities?.OrderByDescending(q => (int)q).FirstOrDefault() ?? TidalQuality.Lossless;
        var quality = DetermineQualityString(bestQuality);

        return new ReleaseInfo
        {
            Guid = $"tidal:album:{album.Id}",
            Title = $"{artistName} - {albumTitle} [{quality}]",
            Artist = artistName,
            Album = albumTitle,
            PublishDate = releaseDate,
            DownloadUrl = $"tidal://album/{album.Id}",
            InfoUrl = $"https://tidal.com/browse/album/{album.Id}",
            Size = EstimateAlbumSize(album, bestQuality)
        };
    }

    private static string DetermineQualityString(TidalQuality quality)
    {
        return quality switch
        {
            TidalQuality.HiRes => "Hi-Res FLAC 24bit",
            TidalQuality.Lossless => "FLAC 16bit",
            TidalQuality.High => "AAC 320kbps",
            _ => "AAC 96kbps"
        };
    }

    private static long EstimateAlbumSize(TidalAlbumInfo album, TidalQuality quality)  
    {
        // Estimate size based on track count and quality
        // Average track: 4 minutes, FLAC: ~1000 kbps, AAC HQ: ~320 kbps        
        var trackCount = album.Tracks?.Count > 0 ? album.Tracks.Count : 12; // Default to 12 tracks when unknown/empty
        var avgTrackDurationSeconds = 240; // 4 minutes average
        var totalDurationSeconds = trackCount * avgTrackDurationSeconds;
        var bitrateKbps = quality >= TidalQuality.Lossless ? 1000 : 320;

        return (long)(totalDurationSeconds * bitrateKbps * 125); // Convert to bytes
    }
}
