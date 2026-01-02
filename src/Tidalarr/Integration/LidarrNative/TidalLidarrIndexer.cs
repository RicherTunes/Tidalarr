using FluentValidation.Results;
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
public class TidalLidarrIndexer(
    IHttpClient httpClient,
    IIndexerStatusService indexerStatusService,
    IConfigService configService,
    IParsingService parsingService,
    Logger logger) : HttpIndexerBase<TidalLidarrIndexerSettings>(httpClient, indexerStatusService, configService, parsingService, logger)
{
    public override string Name => "Tidalarr";
    public override string Protocol => nameof(TidalarrDownloadProtocol);
    public override bool SupportsRss => false;
    public override bool SupportsSearch => true;
    public override int PageSize => 100;

    private new readonly Logger _logger = logger;
    private IServiceProvider _serviceProvider;
    private bool _servicesInitialized;

    /// <summary>
    /// Initialize Tidal services from TidalModule when first needed.
    /// </summary>
    private void EnsureServicesInitialized()
    {
        if (this._servicesInitialized) return;

        try
        {
            ServiceCollection services = new ServiceCollection();

            // Register settings from Lidarr configuration
            TidalIndexerSettings indexerSettings = new TidalIndexerSettings
            {
                ConfigPath = Settings.ConfigPath,
                RedirectUrl = Settings.RedirectUrl,
                TidalMarket = Settings.TidalMarket,
                EarlyReleaseLimit = Settings.EarlyReleaseLimit,
                EnableCache = Settings.EnableCache,
                CacheDuration = Settings.CacheDuration
            };
            _ = services.AddSingleton(indexerSettings);
            _ = services.AddSingleton(new TidalarrSettings
            {
                ConfigPath = Settings.ConfigPath,
                RedirectUrl = Settings.RedirectUrl,
                TidalMarket = Settings.TidalMarket
            });

            // Register all Tidal services
            TidalModule.RegisterServices(services);

            this._serviceProvider = services.BuildServiceProvider();
            this._servicesInitialized = true;
            this._logger.Debug("Tidal services initialized successfully");
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Failed to initialize Tidal services");
            throw;
        }
    }

    public override IIndexerRequestGenerator GetRequestGenerator()
    {
        // We use a special request generator that encodes the search query
        // The actual search happens in the parser using our services
        return new TidalLidarrRequestGenerator(Settings, this._logger);
    }

    public override IParseIndexerResponse GetParser()
    {
        EnsureServicesInitialized();
        return new TidalLidarrParser(Settings, this._serviceProvider, this._logger);
    }

    protected override async Task<IList<ReleaseInfo>> FetchReleases(
        Func<IIndexerRequestGenerator, IndexerPageableRequestChain> pageableRequestChainSelector,
        bool isRecent = false)
    {
        EnsureServicesInitialized();

        List<ReleaseInfo> releases = new List<ReleaseInfo>();

        // Ensure valid session early so Lidarr reports a clear authentication error.
        IStreamingAuthManager? authManager = this._serviceProvider.GetService<IStreamingAuthManager>();
        if (authManager != null)
        {
            await authManager.EnsureValidSessionAsync().ConfigureAwait(false);
        }

        TidalSearchService? searchService = this._serviceProvider.GetService<TidalSearchService>();
        if (searchService == null)
        {
            this._logger.Error("TidalSearchService not available");
            return releases;
        }

        IIndexerRequestGenerator requestGenerator = GetRequestGenerator();
        IndexerPageableRequestChain requestChain = pageableRequestChainSelector(requestGenerator);

        foreach (IndexerPageableRequest? tier in requestChain.GetAllTiers())
        {
            foreach (IndexerRequest? request in tier)
            {
                string requestUrl = request.HttpRequest?.Url?.ToString() ?? string.Empty;
                if (!TryExtractSearchQuery(requestUrl, out string? query))
                {
                    this._logger.Warn("Unexpected request URL format: {0}", requestUrl);
                    continue;
                }

                try
                {
                    TidalSearchResults searchResults = await searchService
                        .SearchWithQualityDetectionAsync(query, TidalQuality.Lossless)
                        .ConfigureAwait(false);

                    if (searchResults.Albums == null || searchResults.Albums.Count == 0)
                    {
                        continue;
                    }

                    foreach (TidalAlbumInfo album in searchResults.Albums)
                    {
                        ReleaseInfo release = TidalLidarrParser.ConvertToReleaseInfoStatic(album);
                        if (release != null)
                        {
                            releases.Add(release);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this._logger.Warn(ex, "Tidal search failed for query: {0}", query);
                }
            }
        }

        // Ensure Lidarr can attribute grabs to this indexer.
        int indexerId = Definition?.Id ?? 0;
        string? indexerName = Definition?.Name;

        foreach (ReleaseInfo release in releases)
        {
            release.IndexerId = indexerId;
            if (string.IsNullOrWhiteSpace(release.Indexer) && !string.IsNullOrWhiteSpace(indexerName))
            {
                release.Indexer = indexerName;
            }
        }

        // Deduplicate by Guid (same query can appear in multiple tiers).
        return [.. releases
            .Where(r => !string.IsNullOrWhiteSpace(r.Guid))
            .GroupBy(r => r.Guid)
            .Select(g => g.First())];
    }

    private static bool TryExtractSearchQuery(string requestUrl, out string query)
    {
        query = string.Empty;

        if (!requestUrl.StartsWith("tidal://search", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        System.Collections.Specialized.NameValueCollection queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        string? q = queryParams["query"];
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
            this._logger.Info("Testing Tidalarr indexer connection...");

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
                    _ = Directory.CreateDirectory(Settings.ConfigPath);
                    this._logger.Info($"Created config directory: {Settings.ConfigPath}");
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
            IStreamingAuthManager? authManager = this._serviceProvider.GetService<IStreamingAuthManager>();
            ITidalAuth? authService = this._serviceProvider.GetService<ITidalAuth>();

            if (authService != null)
            {
                try
                {
                    // Try to get valid tokens
                    _ = await authService.GetValidTokensAsync();
                    this._logger.Debug("Tidal authentication session is valid");
                }
                catch (InvalidOperationException authEx)
                {
                    this._logger.Debug(authEx, "No valid Tidal session - checking if we can authenticate...");

                    // No valid tokens - try to exchange redirect URL if provided
                    if (hasRedirectUrl)
                    {
                        bool exchangeResult = await TryExchangeAuthorizationCode(authService, failures);
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
            TidalSearchService? searchService = this._serviceProvider.GetService<TidalSearchService>();
            if (searchService != null)
            {
                TidalSearchResults testResults = await searchService.SearchWithQualityDetectionAsync("test", TidalQuality.Lossless);
                this._logger.Info($"Test search completed. Found {testResults.Albums?.Count ?? 0} albums.");
            }

            this._logger.Info("Tidalarr indexer test completed successfully");
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Tidalarr indexer test failed");
            failures.Add(new ValidationFailure("Test", $"Test failed: {ex.Message}"));
        }
    }

    private async Task<bool> TryExchangeAuthorizationCode(ITidalAuth authService, List<ValidationFailure> failures)
    {
        try
        {
            // Parse the redirect URL to extract authorization code
            TidalCallbackResult callbackResult = authService.ParseCallbackUrl(Settings.RedirectUrl);
            if (!callbackResult.IsSuccess)
            {
                this._logger.Warn($"Failed to parse redirect URL: {callbackResult.ErrorMessage}");
                failures.Add(new ValidationFailure("RedirectUrl", callbackResult.ErrorMessage ?? "Invalid redirect URL"));
                return false;
            }

            // Load PKCE state from disk
            PKCEStateStore pkceStore = new PKCEStateStore(Settings.ConfigPath);
            PKCEState? pkceState = await pkceStore.LoadStateAsync();

            if (pkceState == null)
            {
                this._logger.Warn("No PKCE state found - auth URL may have expired. Generating new one.");
                await GenerateOAuthAuthUrl(authService, failures);
                return false;
            }

            // Validate state matches
            if (pkceState.State != callbackResult.State)
            {
                this._logger.Warn("PKCE state mismatch - possible CSRF attack or stale auth URL. Generating new one.");
                await GenerateOAuthAuthUrl(authService, failures);
                return false;
            }

            // Exchange authorization code for tokens
            this._logger.Info("Exchanging authorization code for tokens...");
            TidalTokens tokens = await authService.ExchangeCodeAsync(callbackResult.AuthCode, pkceState.CodeVerifier);

            if (tokens == null || string.IsNullOrWhiteSpace(tokens.AccessToken))
            {
                failures.Add(new ValidationFailure("Authentication", "Token exchange failed - received invalid tokens"));
                return false;
            }

            // Clean up PKCE state after successful exchange
            await pkceStore.DeleteStateAsync();

            this._logger.Info("Successfully authenticated with Tidal!");
            return true;
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Failed to exchange authorization code");
            failures.Add(new ValidationFailure("Authentication", $"Token exchange failed: {ex.Message}"));
            return false;
        }
    }

    private async Task GenerateOAuthAuthUrl(ITidalAuth authService, List<ValidationFailure> failures)
    {
        try
        {
            // Generate new auth URL + PKCE state
            TidalAuthUrl authUrlData = await authService.GenerateAuthUrlAsync();

            // Persist PKCE state for later token exchange
            PKCEStateStore pkceStore = new PKCEStateStore(Settings.ConfigPath);
            PKCEState pkceState = new PKCEState(
                authUrlData.AuthorizationUrl,
                authUrlData.CodeVerifier,
                authUrlData.State,
                authUrlData.ClientUniqueKey,
                DateTime.UtcNow);
            await pkceStore.SaveStateAsync(pkceState);

            this._logger.Info("Generated OAuth authorization URL for Tidal authentication.");

            // Provide clear instructions with the auth URL in the error message
            failures.Add(new ValidationFailure("RedirectUrl",
                $"Authentication required. Copy this URL, open in browser, log in, then paste the redirect URL here: {authUrlData.AuthorizationUrl}"));
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Failed to generate OAuth authorization URL");
            failures.Add(new ValidationFailure("Authentication", $"Failed to generate auth URL: {ex.Message}"));
        }
    }
}

/// <summary>
/// Request generator for Tidal API searches.
/// Generates placeholder requests - actual search happens in parser.
/// </summary>
public class TidalLidarrRequestGenerator(TidalLidarrIndexerSettings settings, Logger logger) : IIndexerRequestGenerator
{
    private readonly TidalLidarrIndexerSettings _settings = settings;
    private readonly Logger _logger = logger;

    public IndexerPageableRequestChain GetRecentRequests()
    {
        IndexerPageableRequestChain chain = new IndexerPageableRequestChain();
        // Tidal doesn't have a traditional RSS feed
        return chain;
    }

    public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
    {
        IndexerPageableRequestChain chain = new IndexerPageableRequestChain();

        string searchTerm = $"{searchCriteria.ArtistQuery} {searchCriteria.AlbumQuery}".Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            chain.Add(GetSearchRequests(searchTerm));
        }

        return chain;
    }

    public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria)
    {
        IndexerPageableRequestChain chain = new IndexerPageableRequestChain();

        if (!string.IsNullOrWhiteSpace(searchCriteria.ArtistQuery))
        {
            chain.Add(GetSearchRequests(searchCriteria.ArtistQuery));
        }

        return chain;
    }

    private IEnumerable<IndexerRequest> GetSearchRequests(string searchTerm)
    {
        this._logger.Debug($"Generating Tidal search request for: {searchTerm}");

        // Create a placeholder URL that encodes the search query
        // The actual search is performed by the parser using TidalSearchService
        string encodedQuery = Uri.EscapeDataString(searchTerm);
        string requestUrl = $"tidal://search?query={encodedQuery}";

        HttpRequest request = new HttpRequest(requestUrl);
        request.Headers.Accept = "application/json";

        yield return new IndexerRequest(request);
    }
}

/// <summary>
/// Parser for Tidal search results.
/// Uses TidalSearchService to perform actual searches and converts results to Lidarr format.
/// </summary>
public class TidalLidarrParser(TidalLidarrIndexerSettings settings, IServiceProvider serviceProvider, Logger logger) : IParseIndexerResponse
{
    private readonly TidalLidarrIndexerSettings _settings = settings;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly Logger _logger = logger;

    public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
    {
        List<ReleaseInfo> releases = new List<ReleaseInfo>();

        try
        {
            // Extract search query from the placeholder URL
            string requestUrl = indexerResponse.Request?.Url?.ToString() ?? "";
            if (!requestUrl.StartsWith("tidal://search"))
            {
                this._logger.Warn("Unexpected request URL format: {0}", requestUrl);
                return releases;
            }

            Uri uri = new Uri(requestUrl);
            System.Collections.Specialized.NameValueCollection queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            string? searchQuery = queryParams["query"];

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                this._logger.Warn("No search query found in request");
                return releases;
            }

            // Perform actual search using TidalSearchService
            TidalSearchService? searchService = this._serviceProvider.GetService<TidalSearchService>();
            if (searchService == null)
            {
                this._logger.Error("TidalSearchService not available");
                return releases;
            }

            // Execute search synchronously (we're in a sync context)
            Task<TidalSearchResults> searchTask = searchService.SearchWithQualityDetectionAsync(searchQuery, TidalQuality.Lossless);
            TidalSearchResults searchResults = searchTask.GetAwaiter().GetResult();

            if (searchResults.Albums == null || searchResults.Albums.Count == 0)
            {
                this._logger.Debug("No albums found for query: {0}", searchQuery);
                return releases;
            }

            // Convert Tidal albums to Lidarr ReleaseInfo
            foreach (TidalAlbumInfo album in searchResults.Albums)
            {
                try
                {
                    ReleaseInfo release = ConvertToReleaseInfoStatic(album);
                    if (release != null)
                    {
                        releases.Add(release);
                    }
                }
                catch (Exception ex)
                {
                    this._logger.Warn(ex, "Failed to convert album: {0}", album.Title);
                }
            }

            this._logger.Debug("Parsed {0} releases from Tidal search", releases.Count);
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Failed to parse Tidal search response");
        }

        return releases;
    }

    internal static ReleaseInfo ConvertToReleaseInfoStatic(TidalAlbumInfo album)
    {
        if (album == null) return null;

        string artistName = album.Artists?.FirstOrDefault() ?? "Unknown Artist";
        string albumTitle = album.Title ?? "Unknown Album";
        DateTime releaseDate = album.ReleaseDate;

        // Determine quality from available qualities
        TidalQuality bestQuality = album.AvailableQualities?.OrderByDescending(q => (int)q).FirstOrDefault() ?? TidalQuality.Lossless;
        string quality = DetermineQualityString(bestQuality);

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
            TidalQuality.Low => "AAC 96kbps",
            _ => "AAC 96kbps"
        };
    }

    private static long EstimateAlbumSize(TidalAlbumInfo album, TidalQuality quality)
    {
        // Estimate size based on track count and quality
        // Average track: 4 minutes, FLAC: ~1000 kbps, AAC HQ: ~320 kbps        
        int trackCount = album.Tracks?.Count > 0 ? album.Tracks.Count : 12; // Default to 12 tracks when unknown/empty
        int avgTrackDurationSeconds = 240; // 4 minutes average
        int totalDurationSeconds = trackCount * avgTrackDurationSeconds;
        int bitrateKbps = quality >= TidalQuality.Lossless ? 1000 : 320;

        return totalDurationSeconds * bitrateKbps * 125; // Convert to bytes
    }
}
