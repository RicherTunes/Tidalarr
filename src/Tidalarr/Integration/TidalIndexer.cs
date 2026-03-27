using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Base;
using Lidarr.Plugin.Abstractions.Contracts;
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
    private readonly IAuthFailureHandler? _authHandler;
    private readonly IIndexerStatusReporter? _statusReporter;

    protected override string ServiceName => "Tidal";
    protected override string ProtocolName => "tidal";

    public TidalIndexer(
        TidalSearchService searchService,
        ITidalCore apiClient,
        TidalIndexerSettings settings,
        ILogger? logger = null,
        Lidarr.Plugin.Common.Interfaces.IStreamingTokenProvider? tokenProvider = null,
        IAuthFailureHandler? authHandler = null,
        IIndexerStatusReporter? statusReporter = null)
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

        this._authHandler = authHandler;
        this._statusReporter = statusReporter;
    }

    protected override async Task<bool> AuthenticateAsync()
    {
        try
        {
            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportStatusAsync(IndexerStatus.Authenticating);
            }

            bool result = await this._apiClient.IsAuthenticatedAsync();

            if (result && this._authHandler is not null)
            {
                await this._authHandler.HandleSuccessAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Tidal authentication failed");

            if (this._authHandler is not null)
            {
                await this._authHandler.HandleFailureAsync(new AuthFailure
                {
                    ErrorCode = "TIDAL_AUTH",
                    Message = ex.Message,
                    CanReauthenticate = true
                });
            }

            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportErrorAsync(ex);
            }

            return false;
        }
    }

    protected override async Task<List<StreamingAlbum>> SearchAlbumsAsync(string searchTerm)
    {
        try
        {
            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportStatusAsync(IndexerStatus.Searching, searchTerm);
            }

            TidalSearchResults results = await this._searchService.SearchWithQualityDetectionAsync(searchTerm, TidalQuality.Lossless);
            List<StreamingAlbum> albums = results.Albums?
                .Select(this._mapper.ToStreamingAlbum)
                .Where(a => a is not null)
                .Select(a => a!)
                .ToList() ?? [];

            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportStatusAsync(IndexerStatus.Idle);
            }

            return albums;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to search albums for: {Search}", searchTerm);

            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportErrorAsync(ex);
            }

            throw;
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
            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportStatusAsync(IndexerStatus.Searching, searchTerm);
            }

            TidalSearchResults results = await this._searchService.SearchWithQualityDetectionAsync(searchTerm, TidalQuality.Lossless);
            List<StreamingTrack> tracks = results.Tracks?
                .Select(this._mapper.ToStreamingTrack)
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList() ?? [];

            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportStatusAsync(IndexerStatus.Idle);
            }

            return tracks;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to search tracks for: {Search}", searchTerm);

            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportErrorAsync(ex);
            }

            throw;
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

            // Mapping returned null — return a safe default to satisfy non-nullable contract
            return new StreamingAlbum
            {
                Id = albumId,
                Title = string.Empty,
                Artist = new StreamingArtist { Id = string.Empty, Name = string.Empty }
            };
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to get album details for: {AlbumId}", albumId);

            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportErrorAsync(ex);
            }

            throw;
        }
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

            if (this._statusReporter is not null)
            {
                await this._statusReporter.ReportErrorAsync(ex);
            }

            throw;
        }
    }

    // Inject OAuth-enabled client into base when it performs HTTP
    protected override HttpClient GetHttpClient()
    {
        return this._httpClient;
    }
}




