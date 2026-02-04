using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Base;
using Lidarr.Plugin.Common.Abstractions.Llm;
using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Core.Mappers;
using Lidarr.Plugin.Abstractions.Results;

namespace Tidalarr.Integration;

public class TidalIndexer : BaseStreamingIndexer<TidalIndexerSettings>
{
    private readonly TidalSearchService _searchService;
    private readonly ITidalCore _apiClient;
    private readonly TidalModelMapper _mapper;
    private readonly HttpClient _httpClient;

    protected override string ServiceName => "Tidal";
    protected override string ProtocolName => "tidal";

    public TidalIndexer(
        TidalSearchService searchService,
        ITidalCore apiClient,
        TidalIndexerSettings settings,
        ILogger? logger = null,
        Lidarr.Plugin.Common.Interfaces.IStreamingTokenProvider? tokenProvider = null)
        : base(settings, logger!)
    {
        this._searchService = searchService;
        this._apiClient = apiClient;
        this._mapper = new TidalModelMapper();
        // Provide an OAuth-enabled HttpClient for base operations (if used)
        if (tokenProvider != null)
        {
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
            ILogger ologger = loggerFactory.CreateLogger("OAuthDelegatingHandler");
            Lidarr.Plugin.Common.Services.Http.OAuthDelegatingHandler handler = new(tokenProvider, ologger)
            {
                InnerHandler = new HttpClientHandler()
            };
            this._httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(100)
            };
        }
        else
        {
            this._httpClient = new HttpClient();
        }
    }

    protected override async Task<bool> AuthenticateAsync()
    {
        try { return await this._apiClient.IsAuthenticatedAsync(); }
        catch (Exception ex) { Logger?.LogError(ex, "Tidal authentication failed"); return false; }
    }

    protected override async Task<List<StreamingAlbum>> SearchAlbumsAsync(string searchTerm)
    {
        try
        {
            TidalSearchResults results = await this._searchService.SearchWithQualityDetectionAsync(searchTerm, TidalQuality.Lossless);
            return results.Albums?
                .Select(this._mapper.ToStreamingAlbum)
                .Where(a => a is not null)
                .Select(a => a!)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to search albums for: {Search}", searchTerm);
            return [];
        }
    }

    protected override Task<List<StreamingTrack>> SearchTracksAsync(string searchTerm)
    {
        return SearchTracksInternalAsync(searchTerm);
    }

    internal async Task<List<StreamingTrack>> SearchTracksInternalAsync(string searchTerm)
    {
        try
        {
            TidalSearchResults results = await this._searchService.SearchWithQualityDetectionAsync(searchTerm, TidalQuality.Lossless);
            return results.Tracks?
                .Select(this._mapper.ToStreamingTrack)
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to search tracks for: {Search}", searchTerm);
            return [];
        }
    }

    protected override Task<StreamingAlbum> GetAlbumDetailsAsync(string albumId)
    {
        return GetAlbumDetailsInternalAsync(albumId);
    }

    internal async Task<StreamingAlbum> GetAlbumDetailsInternalAsync(string albumId)
    {
        try
        {
            TidalAlbumInfo tidalAlbum = await this._apiClient.GetAlbumAsync(albumId);
            StreamingAlbum mapped = this._mapper.ToStreamingAlbum(tidalAlbum);
            if (mapped != null)
            {
                return mapped;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to get album details for: {AlbumId}", albumId);
        }
        // Return a safe default to satisfy non-nullable contract
        return new StreamingAlbum
        {
            Id = albumId,
            Title = string.Empty,
            Artist = new StreamingArtist { Id = string.Empty, Name = string.Empty }
        };
    }

    protected override ValidationResult ValidateSettings(TidalIndexerSettings settings)
    {
        ValidationResult result = new();
        if (string.IsNullOrEmpty(settings.TidalMarket))
        {
            result.Errors.Add(new ValidationFailure("TidalMarket", "Tidal market is required"));
        }

        if (string.IsNullOrEmpty(settings.ConfigPath))
        {
            result.Errors.Add(new ValidationFailure("ConfigPath", "Config path is required"));
        }

        return result;
    }

    // Diagnostics-first settings validation with stable IDs (Common result)
    internal PluginOperationResult<Dictionary<string, string>> ValidateSettingsWithDiagnostics()
    {
        const string OK = "IX000";     // Settings valid
        const string INVALID_CODE = "IX100"; // Settings invalid

        ValidationResult validation = Settings.ValidateFluent();
        if (validation.IsValid)
        {
            return PluginOperationResult<Dictionary<string, string>>.Success(new()
            {
                ["id"] = OK,
                ["service"] = ServiceName
            });
        }

        string[] codes = [.. validation.Errors
            .Where(e => !string.IsNullOrWhiteSpace(e.ErrorCode))
            .Select(e => e.ErrorCode)
            .Distinct()];

        return PluginOperationResult<Dictionary<string, string>>.Failure(new PluginError(
            PluginErrorCode.ValidationFailed,
            "Settings failed validation",
            null,
            new Dictionary<string, string>
            {
                ["id"] = INVALID_CODE,
                ["service"] = ServiceName,
                ["errors"] = string.Join(",", codes)
            }));
    }

    // Diagnostics-first initialize that checks validation + auth and returns stable ID
    internal async Task<PluginOperationResult<Dictionary<string, string>>> InitializeWithDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        const string OK = "IX000";       // Initialization OK
        const string AUTHFAIL = "IX200";  // Authentication failed

        PluginOperationResult<Dictionary<string, string>> settingsResult = ValidateSettingsWithDiagnostics();
        if (!settingsResult.IsSuccess)
        {
            return settingsResult;
        }

        try
        {
            bool authed = await this._apiClient.IsAuthenticatedAsync().ConfigureAwait(false);
            return !authed
                ? PluginOperationResult<Dictionary<string, string>>.Failure(new PluginError(
                    PluginErrorCode.Unauthorized,
                    "Authentication failed",
                    null,
                    new Dictionary<string, string>
                    {
                        ["id"] = AUTHFAIL,
                        ["service"] = ServiceName
                    }))
                : PluginOperationResult<Dictionary<string, string>>.Success(new()
                {
                    ["id"] = OK,
                    ["service"] = ServiceName
                });
        }
        catch (Exception ex)
        {
            return PluginOperationResult<Dictionary<string, string>>.Failure(new PluginError(
                PluginErrorCode.Unauthorized,
                ex.Message,
                ex,
                new Dictionary<string, string>
                {
                    ["id"] = AUTHFAIL,
                    ["service"] = ServiceName
                }));
        }
    }

    public async Task<List<StreamingSearchResult>> SearchEnhancedAsync(string query)
    {
        try
        {
            TidalSearchResults results = await this._searchService.SearchWithQualityDetectionAsync(query, TidalQuality.Lossless);
            return this._mapper.ToStreamingSearchResults(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Enhanced search failed for: {Query}", query);
            return [];
        }
    }

    // Inject OAuth-enabled client into base when it performs HTTP
    protected override HttpClient GetHttpClient()
    {
        return this._httpClient;
    }

    // DIAG-02: Standardized test connection method returning ProviderHealthResult
    /// <summary>
    /// Performs a test connection check with extended diagnostics fields.
    /// Returns ProviderHealthResult with provider, authMethod, model, and errorCode fields.
    /// </summary>
    public async Task<ProviderHealthResult> TestConnectionAsync()
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Validate settings first
            ValidationResult validation = ValidateSettings(Settings);
            if (!validation.IsValid)
            {
                var errorCode = validation.Errors.FirstOrDefault()?.ErrorCode ?? "INVALID_SETTINGS";
                var statusMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));

                return ProviderHealthResult.Unhealthy(
                    $"Settings validation failed: {statusMessage}",
                    DateTime.UtcNow - startTime,
                    "tidal",
                    "oauth",
                    "quality_detect",
                    errorCode
                );
            }

            // Authenticate with Tidal service
            bool isAuthenticated = await this._apiClient.IsAuthenticatedAsync().ConfigureAwait(false);

            if (!isAuthenticated)
            {
                return ProviderHealthResult.Unhealthy(
                    "Authentication failed - Tidal API returned unauthenticated response",
                    DateTime.UtcNow - startTime,
                    "tidal",
                    "oauth",
                    "quality_detect",
                    "AUTH_FAILED"
                );
            }

            // Test connection successful
            return ProviderHealthResult.Healthy(
                DateTime.UtcNow - startTime,
                "tidal",
                "oauth",
                "quality_detect"
            );
        }
        catch (Exception ex)
        {
            return ProviderHealthResult.Unhealthy(
                $"Test connection failed with exception: {ex.Message}",
                DateTime.UtcNow - startTime,
                "tidal",
                "oauth",
                "quality_detect",
                "EXCEPTION"
            );
        }
    }

    // Backward compatibility: Original Test(List<ValidationFailure>) pattern
    /// <summary>
    /// Original test pattern for backward compatibility with Lidarr integration.
    /// Returns IndexerEstablishConnectionTestResult with validation failures.
    /// </summary>
    public List<ValidationFailure> Test(List<ValidationFailure> failures)
    {
        // This is a wrapper around ValidateSettings for backward compatibility
        var validation = ValidateSettings(Settings);
        return validation.Errors.ToList();
    }
}




