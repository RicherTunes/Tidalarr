using FluentValidation.Results;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Common.HostBridge;
using Lidarr.Plugin.Common.Observability;
using Lidarr.Plugin.Common.Services.Authentication;
using Lidarr.Plugin.Common.Services.Bridge;
using Lidarr.Plugin.Common.Services.Diagnostics;
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

    // Resolved from the process-wide TidalIndexerRuntimeCache on first call (or on credential change).
    // Captured as a local field so callers within a single Lidarr invocation see a consistent runtime.
    private TidalIndexerRuntime? _runtime;

    /// <summary>
    /// Resolve (or lazily build) the runtime for the current settings. Returns the cached runtime
    /// if credentials haven't changed; builds a fresh one and parks the old one in the graveyard
    /// otherwise. Returns null when ConfigPath is empty.
    /// </summary>
    private async Task<TidalIndexerRuntime?> GetRuntimeAsync(CancellationToken ct = default)
    {
        TidalIndexerRuntime? runtime = await TidalIndexerRuntimeCache.Shared
            .GetAsync(Settings, ct)
            .ConfigureAwait(false);
        this._runtime = runtime;
        return runtime;
    }

    /// <summary>
    /// Synchronous shim used by callers in sync host-contract paths (e.g. <see cref="GetParser"/>).
    /// HttpIndexerBase.Test(List&lt;ValidationFailure&gt;) is a protected void — no async override exists
    /// in this version of Lidarr's plugins branch. Task.Run avoids deadlock when a
    /// SynchronizationContext captures the calling thread. Credential-change invalidation still fires
    /// through the cache.
    /// </summary>
    private TidalIndexerRuntime? EnsureServicesInitialized()
    {
        // SYNC-OVER-ASYNC: callers (GetParser) are sync Lidarr host contracts.
        // Task.Run avoids deadlock when a SynchronizationContext captures the thread.
        TidalIndexerRuntime? runtime = Task.Run(() => GetRuntimeAsync()).GetAwaiter().GetResult();
        if (runtime is null)
        {
            this._logger.Warn("Tidal indexer runtime not available (ConfigPath empty?)");
        }
        else
        {
            this._logger.Debug("Tidal services initialized successfully");
        }
        return runtime;
    }

    public override IIndexerRequestGenerator GetRequestGenerator()
    {
        // We use a special request generator that encodes the search query
        // The actual search happens in the parser using our services
        return new TidalLidarrRequestGenerator(Settings, this._logger);
    }

    public override IParseIndexerResponse GetParser()
    {
        TidalIndexerRuntime? rt = EnsureServicesInitialized();
        IServiceProvider sp = rt?.ServiceProvider
            ?? throw new InvalidOperationException("Tidal indexer runtime unavailable — ConfigPath may be empty.");
        return new TidalLidarrParser(Settings, sp, this._logger);
    }

    protected override async Task<IList<ReleaseInfo>> FetchReleases(
        Func<IIndexerRequestGenerator, IndexerPageableRequestChain> pageableRequestChainSelector,
        bool isRecent = false)
    {
        using PluginLogContext ctx = PluginLogContext.Push("Tidalarr", "Search", provider: "tidal:api");

        TidalIndexerRuntime? rt = await GetRuntimeAsync().ConfigureAwait(false);
        if (rt is null)
        {
            this._logger.Error("Tidal indexer runtime unavailable (ConfigPath empty?)");
            return [];
        }

        IServiceProvider sp = rt.ServiceProvider;

        // AuthFailureGate: if a prior call observed 401/403 and latched the gate, and we
        // don't have a fresh probe slot, return empty immediately. This is the qobuzarr-
        // incident fix — Lidarr's search loop fans out searches at full rate and a dead
        // session would otherwise burn the API quota / risk IP-ban.
        if (IsAuthShortCircuited(sp))
        {
            this._logger.Debug("Short-circuiting Tidal search; auth latched bad and probe slot exhausted");
            return [];
        }

        List<ReleaseInfo> releases = [];

        // Ensure valid session early so Lidarr reports a clear authentication error.
        IStreamingAuthManager? authManager = sp.GetService<IStreamingAuthManager>();
        if (authManager != null)
        {
            await authManager.EnsureValidSessionAsync().ConfigureAwait(false);
        }

        TidalSearchService? searchService = sp.GetService<TidalSearchService>();
        if (searchService == null)
        {
            this._logger.Error("TidalSearchService not available");
            return releases;
        }

        IIndexerRequestGenerator requestGenerator = GetRequestGenerator();
        IndexerPageableRequestChain requestChain = pageableRequestChainSelector(requestGenerator);

        int attempted = 0;
        int succeeded = 0;
        Exception? lastError = null;
        foreach (IndexerPageableRequest? tier in requestChain.GetAllTiers())
        {
            foreach (IndexerRequest? request in tier)
            {
                string requestUrl = request.HttpRequest?.Url?.ToString() ?? string.Empty;
                if (!PlaceholderSearchUri.TryExtractQuery(requestUrl, "tidal", out string? query))
                {
                    this._logger.Warn("Unexpected request URL format: {0}", requestUrl);
                    continue;
                }

                attempted++;
                try
                {
                    TidalSearchResults searchResults = await searchService
                        .SearchWithQualityDetectionAsync(query, TidalQuality.Lossless)
                        .ConfigureAwait(false);
                    succeeded++;

                    if (searchResults.Albums == null || searchResults.Albums.Count == 0)
                    {
                        continue;
                    }

                    this._logger.Debug("Tidal search returned {0} albums for query: {1}", searchResults.Albums.Count, query);

                    foreach (TidalAlbumInfo album in searchResults.Albums)
                    {
                        // Create multiple releases per album - one for each available quality
                        List<ReleaseInfo> albumReleases = [.. TidalLidarrParser.ConvertToReleaseInfosStatic(album)];
                        this._logger.Debug("Created {0} releases for album: {1}", albumReleases.Count, album.Title);
                        releases.AddRange(albumReleases);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    RecordAuthOutcomeFromException(sp, ex);
                    this._logger.Warn(ex, "Tidal search failed for query: {0}", query);
                }
            }
        }

        // If EVERY attempted query threw, surface the failure instead of a misleading empty
        // result — otherwise Lidarr can't distinguish "no matches" from "all search calls failed".
        // A query that returns no albums does NOT throw, so genuine empty results are unaffected.
        if (attempted > 0 && succeeded == 0 && lastError is not null)
        {
            throw new InvalidOperationException(
                $"All {attempted} Tidal search request(s) failed; surfacing the error instead of an empty result.",
                lastError);
        }

        this._logger.Debug("Total releases before dedup: {0}", releases.Count);

        // Deduplicate by Guid (same query can appear in multiple tiers).
        List<ReleaseInfo> deduplicated = [.. releases
            .Where(r => !string.IsNullOrWhiteSpace(r.Guid))
            .GroupBy(r => r.Guid)
            .Select(g => g.First())];

        int albumCount = deduplicated.Select(r => r.Album).Distinct().Count();
        int duplicatesRemoved = releases.Count - deduplicated.Count;
        this._logger.Debug("Total releases after dedup: {0}", deduplicated.Count);
        this._logger.Info("Tidal search yielded {0} releases across {1} albums ({2} duplicates removed)",
            deduplicated.Count, albumCount, duplicatesRemoved);

        // CleanupReleases sets IndexerId, Indexer, DownloadProtocol, and IndexerPriority from indexer Definition.
        return CleanupReleases(deduplicated);
    }

    protected override async Task Test(List<ValidationFailure> failures)
    {
        using PluginLogContext ctx = PluginLogContext.Push("Tidalarr", "Test");
        try
        {
            this._logger.Info("Testing Tidalarr indexer connection...");

            // Basic settings validation
            if (string.IsNullOrWhiteSpace(Settings.ConfigPath))
            {
                failures.Add(new ValidationFailure(
                    "ConfigPath",
                    "Config path is required. Default is /config/Tidalarr in Docker, or AppData/Tidalarr (~/.config/Tidalarr on Linux). Tokens are persisted there."));
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

            // Initialize services (async; await gives the cache a chance to invalidate on cred change).
            TidalIndexerRuntime? rt = await GetRuntimeAsync().ConfigureAwait(false);
            if (rt is null)
            {
                failures.Add(new ValidationFailure("ConfigPath", "Tidal runtime could not be initialized — ConfigPath may be empty."));
                return;
            }

            IServiceProvider testSp = rt.ServiceProvider;

            // AuthFailureGate: if the gate is latched bad and no probe slot is available,
            // surface a clear "auth needs attention" failure instead of beating the API.
            // Test() runs in response to a user clicking "Test" in the settings UI so a
            // probe-budget exhausted state is informative (auth is bad, retry after
            // re-credentialing).
            if (IsAuthShortCircuited(testSp))
            {
                failures.Add(new ValidationFailure(
                    "Authentication",
                    "Tidal authentication is latched bad (auth failure observed recently). Re-authenticate by pasting a fresh redirect URL and retry — the gate will auto-recover once a request succeeds."));
                return;
            }

            // Check if we have a valid redirect URL with authorization code
            bool hasRedirectUrl = !string.IsNullOrWhiteSpace(Settings.RedirectUrl);

            // Try to get existing valid session first
            IStreamingAuthManager? authManager = testSp.GetService<IStreamingAuthManager>();
            ITidalAuth? authService = testSp.GetService<ITidalAuth>();

            if (authService != null)
            {
                try
                {
                    // Try to get valid tokens
                    _ = await authService.GetValidTokensAsync().ConfigureAwait(false);
                    this._logger.Debug("Tidal authentication session is valid");
                }
                catch (InvalidOperationException authEx)
                {
                    this._logger.Debug(authEx, "No valid Tidal session - checking if we can authenticate...");

                    // No valid tokens - try to exchange redirect URL if provided
                    if (hasRedirectUrl)
                    {
                        bool exchangeResult = await TryExchangeAuthorizationCode(authService, failures).ConfigureAwait(false);
                        if (!exchangeResult)
                        {
                            return; // Error already added to failures
                        }
                    }
                    else
                    {
                        // No redirect URL - generate auth URL for user
                        await GenerateOAuthAuthUrl(failures).ConfigureAwait(false);
                        return;
                    }
                }
            }

            // Test a simple search
            TidalSearchService? searchService = testSp.GetService<TidalSearchService>();
            if (searchService != null)
            {
                TidalSearchResults testResults = await searchService.SearchWithQualityDetectionAsync("test", TidalQuality.Lossless).ConfigureAwait(false);
                this._logger.Info($"Test search completed. Found {testResults.Albums?.Count ?? 0} albums.");
            }

            this._logger.Info("Tidalarr indexer test completed successfully");
        }
        catch (Exception ex)
        {
            // Best-effort: record auth-class outcomes so subsequent searches short-circuit
            // instead of hammering Tidal until the user re-credentials. testSp may not have
            // been initialized if the failure happened before runtime resolution — handled
            // safely by the helper.
            try
            {
                TidalIndexerRuntime? runtimeForGate = await GetRuntimeAsync().ConfigureAwait(false);
                if (runtimeForGate is not null)
                {
                    RecordAuthOutcomeFromException(runtimeForGate.ServiceProvider, ex);
                }
            }
            catch
            {
                // Recording is purely opportunistic; never let it mask the original failure.
            }

            this._logger.Error(ex, "Tidalarr indexer test failed");
            // HttpExceptionClassifier (Common): categorizes the failure into Auth / RateLimit /
            // Network / Timeout / Server / etc. and returns an actionable user-readable Hint
            // that doesn't leak CLR type names. Routes Auth-class failures to the
            // "Authentication" field so the UI surfaces them in the credential section instead
            // of the generic "Test" bucket. Matches qobuz's adoption pattern at
            // src/API/AdaptiveQobuzApiClient.cs:54 + src/Services/AuthTokenManager.cs:376.
            HttpFailureClassification classification = HttpExceptionClassifier.Classify(ex);
            string failureField = classification.Category == HttpFailureCategory.Auth
                ? "Authentication"
                : "Test";
            failures.Add(new ValidationFailure(failureField, classification.Hint));
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

            // Load PKCE state (code_verifier) needed for token exchange.
            // Like TrevTV's implementation, we skip state validation - it's for CSRF protection
            // which isn't relevant in a manual copy/paste OAuth flow.
            PKCEStateStore pkceStore = new(Settings.ConfigPath);
            PKCEState? pkceState = await pkceStore.LoadStateAsync().ConfigureAwait(false);

            if (pkceState == null)
            {
                this._logger.Warn("No PKCE state found - auth URL may have expired. Generating new one.");
                await GenerateOAuthAuthUrl(failures).ConfigureAwait(false);
                return false;
            }

            if (!PKCEStateStore.IsCallbackStateMatch(pkceState, callbackResult.State))
            {
                this._logger.Warn("OAuth state mismatch - likely a stale URL or different browser tab. Regenerating OAuth URL.");
                PKCEStateStore.RegenerateCodes(Settings.ConfigPath);
                await GenerateOAuthAuthUrl(failures, prefix: "OAuth state mismatch. ").ConfigureAwait(false);
                return false;
            }

            // Exchange authorization code for tokens
            this._logger.Info("Exchanging authorization code for tokens...");
            TidalTokens tokens = await authService.ExchangeCodeAsync(callbackResult.AuthCode, pkceState.CodeVerifier).ConfigureAwait(false);

            if (tokens == null || string.IsNullOrWhiteSpace(tokens.AccessToken))
            {
                // Wave 79 UX: name the most-common cause (stale redirect URL) and the
                // exact recovery action so user doesn't think it's a Tidal outage.
                failures.Add(new ValidationFailure(
                    "Authentication",
                    "Token exchange failed: Tidal returned no valid tokens. The redirect URL is likely stale (used or expired). Click Test, paste the NEW redirect URL from a fresh browser login, and try again."));
                return false;
            }

            // Regenerate PKCE codes after successful exchange so the OAuth URL field
            // shows a fresh URL for any future re-authentication needs.
            PKCEStateStore.RegenerateCodes(Settings.ConfigPath);

            this._logger.Info("Successfully authenticated with Tidal!");
            return true;
        }
        catch (Tidalarr.Domain.Authentication.TidalInvalidGrantException ex)
        {
            // The authorization code was already consumed or has expired.
            // Clear the cached field so the next Test uses whatever fresh URL the
            // user pastes rather than silently re-submitting the dead code.
            this._logger.Warn(ex, "Tidal rejected the authorization code (invalid_grant) — clearing cached redirect URL.");
            Settings.RedirectUrl = string.Empty;

            failures.Add(new ValidationFailure(
                "RedirectUrl",
                ex.Message));
            return false;
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Failed to exchange authorization code");
            // Wave 79 UX: surface the exception type and remind users about the
            // most common recovery action (paste a fresh redirect URL).
            failures.Add(new ValidationFailure(
                "Authentication",
                $"Token exchange failed ({ex.GetType().Name}): {ex.Message}. If this persists, paste a fresh redirect URL from a new browser login."));
            return false;
        }
    }

    private Task GenerateOAuthAuthUrl(List<ValidationFailure> failures, string? prefix = null)
    {
        try
        {
            // Get the OAuth URL from PKCEStateStore - creates state if needed, or returns existing.
            // IMPORTANT: Don't call RegenerateCodes() here - that would overwrite state the user
            // may have already used to authenticate, causing code_verifier mismatch.
            string? authUrl = PKCEStateStore.TryGetOrCreateAuthorizationUrl(Settings.ConfigPath);

            if (string.IsNullOrEmpty(authUrl))
            {
                failures.Add(new ValidationFailure("ConfigPath",
                    "Failed to generate OAuth URL. Ensure Config Path is set to a writable directory."));
                return Task.CompletedTask;
            }

            this._logger.Info("Generated OAuth authorization URL for Tidal authentication (scrubbed: {0}).", Scrub.Url(authUrl));

            // Provide clear instructions with the auth URL in the error message
            failures.Add(new ValidationFailure("RedirectUrl",
                $"{prefix}Authentication required. Copy this URL, open in browser, log in, then paste the redirect URL here: {authUrl}"));
        }
        catch (Exception ex)
        {
            this._logger.Error(ex, "Failed to generate OAuth authorization URL");
            failures.Add(new ValidationFailure("Authentication", $"Failed to generate auth URL: {ex.Message}"));
        }

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // AuthFailureGate helpers
    //
    // Mirror the apple/qobuz pattern (AppleMusicIndexerAdapter.cs:63-104):
    // resolve the gate from the per-call IServiceProvider; if the gate is
    // latched bad AND no probe slot is available, callers should return
    // empty/throw a clear error instead of beating the API.
    //
    // Per-call resolution (rather than ctor-injection) is required because
    // TidalLidarrIndexer's ctor signature is fixed by Lidarr's HttpIndexerBase
    // contract — adding a ctor param would break discovery. The runtime's
    // ServiceProvider is already resolved per-call so the lookup cost is
    // negligible.
    // ------------------------------------------------------------------ //

    private static bool IsAuthShortCircuited(IServiceProvider sp)
        => sp.GetService<AuthFailureGate>()?.ShouldShortCircuit() ?? false;

    private static void RecordAuthOutcomeFromException(IServiceProvider sp, Exception ex)
        => sp.GetService<AuthFailureGate>()?.RecordExceptionOutcome(ex, ClassifyAuthFailure);

    // The only service-specific part: 401/403 latch the gate. TidalInvalidGrantException
    // is intentionally NOT classified here — it drives the "paste a fresh redirect URL"
    // UX, a different recovery path. The short-circuit logic and the Category-A
    // sync-over-async hop now live in Common's AuthFailureGate (ShouldShortCircuit /
    // RecordExceptionOutcome), lifted from the byte-identical apple/qobuz/tidal helpers.
    private static AuthFailure? ClassifyAuthFailure(Exception ex)
        => ex is System.Net.Http.HttpRequestException hre
           && hre.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
            ? new AuthFailure { ErrorCode = hre.StatusCode?.ToString(), Message = ex.Message }
            : null;
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
        IndexerPageableRequestChain chain = new();
        // Tidal doesn't have a traditional RSS feed
        return chain;
    }

    public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
    {
        IndexerPageableRequestChain chain = new();

        string searchTerm = $"{searchCriteria.ArtistQuery} {searchCriteria.AlbumQuery}".Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            chain.Add(GetSearchRequests(searchTerm));
        }

        return chain;
    }

    public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria)
    {
        IndexerPageableRequestChain chain = new();

        if (!string.IsNullOrWhiteSpace(searchCriteria.ArtistQuery))
        {
            chain.Add(GetSearchRequests(searchCriteria.ArtistQuery));
        }

        return chain;
    }

    private IEnumerable<IndexerRequest> GetSearchRequests(string searchTerm)
    {
        this._logger.Debug($"Generating Tidal search request for: {searchTerm}");

        // Create a placeholder URL that encodes the search query.
        // The actual search is performed in FetchReleases/ParseResponse via TidalSearchService.
        string requestUrl = PlaceholderSearchUri.Build("tidal", searchTerm);

        HttpRequest request = new(requestUrl);
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
    private static readonly Dictionary<TidalQuality, string> QualityNames = new()
    {
        [TidalQuality.Low] = nameof(TidalQuality.Low),
        [TidalQuality.High] = nameof(TidalQuality.High),
        [TidalQuality.Lossless] = nameof(TidalQuality.Lossless),
        [TidalQuality.HiRes] = nameof(TidalQuality.HiRes),
    };

    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly Logger _logger = logger;

    public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
    {
        List<ReleaseInfo> releases = [];

        try
        {
            // Extract search query from the placeholder URL via Common primitive.
            string requestUrl = indexerResponse.Request?.Url?.ToString() ?? "";
            if (!PlaceholderSearchUri.TryExtractQuery(requestUrl, "tidal", out string? searchQuery))
            {
                this._logger.Warn("Unexpected request URL format: {0}", requestUrl);
                return releases;
            }

            // Perform actual search using TidalSearchService
            TidalSearchService? searchService = this._serviceProvider.GetService<TidalSearchService>();
            if (searchService == null)
            {
                this._logger.Error("TidalSearchService not available");
                return releases;
            }

            // SYNC-OVER-ASYNC: IParseIndexerResponse.ParseResponse is a synchronous Lidarr host contract.
            // FetchReleases (async override) is the primary path; this parser is a fallback used by
            // base-class code paths that process the tidal:// placeholder URLs.
            // Wrapped in Task.Run to avoid deadlock if Lidarr's SynchronizationContext captures the thread.
            TidalSearchResults searchResults = Task.Run(
                () => searchService.SearchWithQualityDetectionAsync(searchQuery, TidalQuality.Lossless))
                .GetAwaiter().GetResult();

            if (searchResults.Albums == null || searchResults.Albums.Count == 0)
            {
                this._logger.Debug("No albums found for query: {0}", searchQuery);
                return releases;
            }

            // Convert Tidal albums to Lidarr ReleaseInfo - create multiple releases per album (one per quality)
            foreach (TidalAlbumInfo album in searchResults.Albums)
            {
                try
                {
                    IEnumerable<ReleaseInfo> albumReleases = ConvertToReleaseInfosStatic(album);
                    releases.AddRange(albumReleases);
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

    /// <summary>
    /// Creates multiple ReleaseInfo entries per album - one for each available quality.
    /// This matches TrevTV's approach where users see all quality options (LOW, HIGH, LOSSLESS, HI_RES).
    /// </summary>
    internal static IEnumerable<ReleaseInfo> ConvertToReleaseInfosStatic(TidalAlbumInfo album)
    {
        if (album == null || string.IsNullOrWhiteSpace(album.Id))
        {
            yield break;
        }

        string artistName = album.Artists?.FirstOrDefault() ?? "Unknown Artist";
        string albumTitle = album.Title ?? "Unknown Album";
        DateTime releaseDate = album.ReleaseDate;
        int year = releaseDate.Year > 1900 ? releaseDate.Year : 0;
        int durationSeconds = EstimateAlbumDurationSeconds(album);

        // Always offer all quality levels like TrevTV does - let the download handle actual availability.
        // This ensures users see all options regardless of what the search API returns.
        TidalQuality[] allQualities = [TidalQuality.Low, TidalQuality.High, TidalQuality.Lossless, TidalQuality.HiRes];

        // Per-album fields are constant across tiers; Common's MultiQualityReleaseBuilder loops the
        // quality specs, composing AlbumReleaseInfoBuilder (per-tier GUID/URL/title) + AlbumSizeEstimator
        // (per-tier size) so the boilerplate lives in one tested place. Tidal-specific bits (the quality
        // ladder, bracket tokens, bitrate-per-quality) are supplied below via AddQuality.
        MultiQualityReleaseBuilder builder = new MultiQualityReleaseBuilder()
            .WithArtist(artistName)
            .WithAlbum(albumTitle)
            .WithYear(year > 0 ? year : null)
            .WithScheme("tidal")
            .WithAlbumId(album.Id)
            .WithDurationSeconds(durationSeconds);

        foreach (TidalQuality quality in allQualities)
        {
            (string formatMarker, string? extraMarker) = DetermineTitleMarkers(quality);
            builder.AddQuality(
                QualityNames.GetValueOrDefault(quality, quality.ToString()),
                formatMarker,
                extraMarker,
                BitrateKbpsForQuality(quality) * 1000.0);
        }

        string infoUrl = $"https://tidal.com/browse/album/{Uri.EscapeDataString(album.Id ?? string.Empty)}";
        foreach (MultiQualityRelease release in builder.Build())
        {
            yield return new ReleaseInfo
            {
                Guid = release.Guid,
                Title = release.Title,
                Artist = artistName,
                Album = albumTitle,
                PublishDate = releaseDate,
                DownloadUrl = release.DownloadUrl,
                InfoUrl = infoUrl,
                Size = release.SizeBytes,
                DownloadProtocol = nameof(TidalarrDownloadProtocol)
            };
        }
    }

    internal static ReleaseInfo? ConvertToReleaseInfoStatic(TidalAlbumInfo album)
    {
        if (album == null || string.IsNullOrWhiteSpace(album.Id))
        {
            return null;
        }

        string artistName = album.Artists?.FirstOrDefault() ?? "Unknown Artist";
        string albumTitle = album.Title ?? "Unknown Album";
        DateTime releaseDate = album.ReleaseDate;

        // Determine quality from available qualities.
        // DefaultIfEmpty guards against empty list — FirstOrDefault() would return Low (0), not Lossless.
        TidalQuality bestQuality = album.AvailableQualities?.OrderByDescending(q => (int)q).DefaultIfEmpty(TidalQuality.Lossless).First() ?? TidalQuality.Lossless;
        (string formatMarker, string? extraMarker) = DetermineTitleMarkers(bestQuality);

        int year = releaseDate.Year > 1900 ? releaseDate.Year : 0;
        (string guid, string downloadUrl, string title) = new AlbumReleaseInfoBuilder()
            .WithArtist(artistName)
            .WithAlbum(albumTitle)
            .WithYear(year > 0 ? year : null)
            .WithFormatMarker(formatMarker)
            .WithExtraMarker(extraMarker)
            .WithScheme("tidal")
            .WithAlbumId(album.Id)
            .WithQualityHint(QualityNames.GetValueOrDefault(bestQuality, bestQuality.ToString()))
            .Build();

        return new ReleaseInfo
        {
            Guid = guid,
            DownloadProtocol = nameof(TidalarrDownloadProtocol),
            Title = title,
            Artist = artistName,
            Album = albumTitle,
            PublishDate = releaseDate,
            DownloadUrl = downloadUrl,
            InfoUrl = $"https://tidal.com/browse/album/{Uri.EscapeDataString(album.Id ?? string.Empty)}",
            Size = EstimateAlbumSize(album, bestQuality)
        };
    }

    private static (string FormatMarker, string? ExtraMarker) DetermineTitleMarkers(TidalQuality quality)
    {
        // Canonical bracket tokens for Lidarr release parsing.
        // Lossless format matches Qobuzarr ([FLAC] [WEB]); hi-res differs
        // because Tidal doesn't expose bitdepth/samplerate like Qobuz does.
        return quality switch
        {
            TidalQuality.HiRes => ("FLAC", (string?)"HIRES"),
            TidalQuality.Lossless => ("FLAC", null),
            TidalQuality.High => ("AAC", (string?)"320"),
            TidalQuality.Low => ("AAC", (string?)"96"),
            _ => ("AAC", null)
        };
    }

    // Tidal's per-quality bitrate ladder (kbps). Tidal-specific — stays local; only the generic
    // duration×bitrate→bytes arithmetic is delegated to Common's AlbumSizeEstimator.
    // FLAC 16-bit/44.1kHz (Lossless): ~1000 kbps; FLAC 24-bit/96kHz (HiRes): ~3000 kbps
    // (2.5-4x larger due to bit depth + sample rate); AAC HQ: ~320 kbps; AAC Low: ~96 kbps.
    private static int BitrateKbpsForQuality(TidalQuality quality) => quality switch
    {
        TidalQuality.HiRes => 3000,
        TidalQuality.Lossless => 1000,
        TidalQuality.High => 320,
        TidalQuality.Low => 96,
        _ => 96
    };

    // Tidal-specific duration estimate: sum actual track durations (defaulting a missing one to 4 min)
    // when any are known, else count * 4-min average (12 tracks when the count is unknown).
    private static int EstimateAlbumDurationSeconds(TidalAlbumInfo album)
    {
        if (album.Tracks?.Count > 0 && album.Tracks.Any(t => t.Duration > 0))
        {
            return album.Tracks.Sum(t => t.Duration > 0 ? t.Duration : 240);
        }

        int trackCount = album.Tracks?.Count > 0 ? album.Tracks.Count : 12;
        return trackCount * 240;
    }

    private static long EstimateAlbumSize(TidalAlbumInfo album, TidalQuality quality)
        => AlbumSizeEstimator.EstimateBytesFromBitrate(
            EstimateAlbumDurationSeconds(album),
            BitrateKbpsForQuality(quality) * 1000.0);
}
