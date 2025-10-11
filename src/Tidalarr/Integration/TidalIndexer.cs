using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Base;
using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Quality;
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
        _searchService = searchService;
        _apiClient = apiClient;
        _mapper = new TidalModelMapper();
        // Provide an OAuth-enabled HttpClient for base operations (if used)
        if (tokenProvider != null)
        {
            var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
            var ologger = loggerFactory.CreateLogger("OAuthDelegatingHandler");
            var handler = new Lidarr.Plugin.Common.Services.Http.OAuthDelegatingHandler(tokenProvider, ologger)
            {
                InnerHandler = new HttpClientHandler()
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(100)
            };
        }
        else
        {
            _httpClient = new HttpClient();
        }
    }

    protected override async Task<bool> AuthenticateAsync()
    {
        try { return await _apiClient.IsAuthenticatedAsync(); }
        catch (Exception ex) { Logger?.LogError(ex, "Tidal authentication failed"); return false; }
    }

    protected override async Task<List<StreamingAlbum>> SearchAlbumsAsync(string searchTerm)
    {
        try
        {
            var results = await _searchService.SearchWithQualityDetectionAsync(searchTerm, TidalQuality.Lossless);
            return results.Albums?
                .Select(_mapper.ToStreamingAlbum)
                .Where(a => a is not null)
                .Select(a => a!)
                .ToList() ?? new();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to search albums for: {Search}", searchTerm);
            return new();
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
            var results = await _searchService.SearchWithQualityDetectionAsync(searchTerm, TidalQuality.Lossless);
            return results.Tracks?
                .Select(_mapper.ToStreamingTrack)
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList() ?? new();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to search tracks for: {Search}", searchTerm);
            return new();
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
            var tidalAlbum = await _apiClient.GetAlbumAsync(albumId);
            var mapped = _mapper.ToStreamingAlbum(tidalAlbum);
            if (mapped != null) return mapped;
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
        var result = new ValidationResult();
        if (string.IsNullOrEmpty(settings.TidalMarket))
            result.Errors.Add(new FluentValidation.Results.ValidationFailure("TidalMarket", "Tidal market is required"));
        if (string.IsNullOrEmpty(settings.ConfigPath))
            result.Errors.Add(new FluentValidation.Results.ValidationFailure("ConfigPath", "Config path is required"));
        return result;
    }

    // Diagnostics-first settings validation with stable IDs (Common result)
    internal PluginOperationResult<Dictionary<string, string>> ValidateSettingsWithDiagnostics()
    {
        const string OK = "IX000";     // Settings valid
        const string INVALID_CODE = "IX100"; // Settings invalid

        var validation = Settings.ValidateFluent();
        if (validation.IsValid)
        {
            return PluginOperationResult<Dictionary<string, string>>.Success(new()
            {
                ["id"] = OK,
                ["service"] = ServiceName
            });
        }

        var codes = validation.Errors
            .Where(e => !string.IsNullOrWhiteSpace(e.ErrorCode))
            .Select(e => e.ErrorCode)
            .Distinct()
            .ToArray();

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

        var settingsResult = ValidateSettingsWithDiagnostics();
        if (!settingsResult.IsSuccess)
        {
            return settingsResult;
        }

        try
        {
            var authed = await _apiClient.IsAuthenticatedAsync().ConfigureAwait(false);
            if (!authed)
            {
                return PluginOperationResult<Dictionary<string, string>>.Failure(new PluginError(
                    PluginErrorCode.Unauthorized,
                    "Authentication failed",
                    null,
                    new Dictionary<string, string>
                    {
                        ["id"] = AUTHFAIL,
                        ["service"] = ServiceName
                    }));
            }
            return PluginOperationResult<Dictionary<string, string>>.Success(new()
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
            var results = await _searchService.SearchWithQualityDetectionAsync(query, TidalQuality.Lossless);
            return _mapper.ToStreamingSearchResults(results);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Enhanced search failed for: {Query}", query);
            return new();
        }
    }

    // Inject OAuth-enabled client into base when it performs HTTP
    protected override HttpClient GetHttpClient() => _httpClient;
}




