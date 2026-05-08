using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;
using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Abstractions.Results;
using FluentValidation.Results;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for TidalIndexer - tests uncovered paths including:
/// - AuthenticateAsync with IAuthFailureHandler and IIndexerStatusReporter
/// - SearchAlbumsAsync/SearchTracksInternalAsync exception paths
/// - GetAlbumDetailsInternalAsync null mapping and exception paths
/// - ValidateSettings direct invocation
/// - ValidateSettingsWithDiagnostics success/failure
/// - InitializeWithDiagnosticsAsync success/auth failure/exception
/// - SearchEnhancedAsync success/exception
/// - GetHttpClient and constructor with tokenProvider
/// </summary>
public class IndexerCovTests
{
    private static TidalIndexerSettings ValidSettings => new()
    {
        RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
        ConfigPath = Path.GetTempPath(),
        TidalMarket = "US"
    };

    #region Helper Stubs

    private class AuthenticatedCore : ITidalCore
    {
        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo(trackId, "Track", ["Artist"], "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow));

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalAlbumInfo(albumId, "Album", ["Artist"], [], [TidalQuality.Lossless], DateTime.UtcNow, "cover", true));

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalSearchResults(
                [new("al1", "Album", ["Artist"], [], [TidalQuality.Lossless], DateTime.UtcNow, "c", true)],
                [new("t1", "Track", ["Artist"], "al1", "Album", 1, 100, TidalQuality.High, true, DateTime.UtcNow)],
                [],
                2,
                false));

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, ["url"], ".flac", "audio/flac", false, null));

        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class ThrowingCore : ITidalCore
    {
        private readonly Exception _authException;

        public ThrowingCore(Exception authException)
        {
            _authException = authException;
        }

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalSearchResults([], [], [], 0, false));

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));

        public Task<bool> IsAuthenticatedAsync() => throw _authException;
    }

    private class SearchThrowingCore : ITidalCore
    {
        private readonly Exception _searchException;

        public SearchThrowingCore(Exception searchException)
        {
            _searchException = searchException;
        }

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => throw _searchException;

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));

        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class AlbumThrowingCore : ITidalCore
    {
        private readonly Exception _albumException;

        public AlbumThrowingCore(Exception albumException)
        {
            _albumException = albumException;
        }

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => throw _albumException;

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalSearchResults([], [], [], 0, false));

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));

        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    /// <summary>
    /// Exposes internal/protected methods for testing
    /// </summary>
    private class TestableTidalIndexer : TidalIndexer
    {
        public TestableTidalIndexer(
            TidalSearchService searchService,
            ITidalCore apiClient,
            TidalIndexerSettings settings,
            ILogger? logger = null,
            Lidarr.Plugin.Common.Interfaces.IStreamingTokenProvider? tokenProvider = null,
            IAuthFailureHandler? authHandler = null,
            IIndexerStatusReporter? statusReporter = null)
            : base(searchService, apiClient, settings, logger, tokenProvider, authHandler, statusReporter)
        {
        }

        public Task<bool> ExposeAuthenticateAsync() => AuthenticateAsync();

        public Task<List<Lidarr.Plugin.Abstractions.Models.StreamingAlbum>> ExposeSearchAlbumsAsync(string searchTerm)
            => SearchAlbumsAsync(searchTerm);

        public Task<List<Lidarr.Plugin.Abstractions.Models.StreamingTrack>> ExposeSearchTracksAsync(string searchTerm)
            => SearchTracksAsync(searchTerm);

        public Task<Lidarr.Plugin.Abstractions.Models.StreamingAlbum> ExposeGetAlbumDetailsAsync(string albumId)
            => GetAlbumDetailsAsync(albumId);

        public ValidationResult ExposeValidateSettings(TidalIndexerSettings settings)
            => ValidateSettings(settings);

        public HttpClient ExposeGetHttpClient() => GetHttpClient();
    }

    #endregion

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_WhenAuthenticated_CallsAuthHandlerSuccess()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockAuthHandler = new Mock<IAuthFailureHandler>();
        var mockStatusReporter = new Mock<IIndexerStatusReporter>();

        var indexer = new TestableTidalIndexer(
            searchService, core, ValidSettings, NullLogger.Instance,
            authHandler: mockAuthHandler.Object, statusReporter: mockStatusReporter.Object);

        // Act
        bool result = await indexer.ExposeAuthenticateAsync();

        // Assert
        Assert.True(result);
        mockAuthHandler.Verify(h => h.HandleSuccessAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockStatusReporter.Verify(r => r.ReportStatusAsync(IndexerStatus.Authenticating, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenException_CallsAuthHandlerFailure()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Auth failed");
        var core = new ThrowingCore(expectedException);
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockAuthHandler = new Mock<IAuthFailureHandler>();
        var mockStatusReporter = new Mock<IIndexerStatusReporter>();

        var indexer = new TestableTidalIndexer(
            searchService, core, ValidSettings, NullLogger.Instance,
            authHandler: mockAuthHandler.Object, statusReporter: mockStatusReporter.Object);

        // Act
        bool result = await indexer.ExposeAuthenticateAsync();

        // Assert
        Assert.False(result);
        mockAuthHandler.Verify(h => h.HandleFailureAsync(
            It.Is<AuthFailure>(f => f.ErrorCode == "TIDAL_AUTH" && f.Message == "Auth failed" && f.CanReauthenticate),
            It.IsAny<CancellationToken>()), Times.Once);
        mockStatusReporter.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == expectedException), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAuthenticatedWithStatusReporter_ReportsAuthenticatingStatus()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatusReporter = new Mock<IIndexerStatusReporter>();

        var indexer = new TestableTidalIndexer(
            searchService, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatusReporter.Object);

        // Act
        await indexer.ExposeAuthenticateAsync();

        // Assert
        mockStatusReporter.Verify(r => r.ReportStatusAsync(IndexerStatus.Authenticating, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SearchAlbumsAsync Exception Tests

    [Fact]
    public async Task SearchAlbumsAsync_WhenException_ReportsErrorAndRethrows()
    {
        // Arrange
        var expectedException = new HttpRequestException("Network error");
        var core = new SearchThrowingCore(expectedException);
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatusReporter = new Mock<IIndexerStatusReporter>();

        var indexer = new TestableTidalIndexer(
            searchService, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatusReporter.Object);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => indexer.ExposeSearchAlbumsAsync("query"));
        Assert.Equal("Network error", thrown.Message);
        mockStatusReporter.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == expectedException), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAlbumsAsync_WithStatusReporter_ReportsSearchingThenIdle()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatusReporter = new Mock<IIndexerStatusReporter>();

        var indexer = new TestableTidalIndexer(
            searchService, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatusReporter.Object);

        // Act
        var result = await indexer.ExposeSearchAlbumsAsync("Daft Punk");

        // Assert
        Assert.NotEmpty(result);
        mockStatusReporter.Verify(r => r.ReportStatusAsync(IndexerStatus.Searching, "Daft Punk", It.IsAny<CancellationToken>()), Times.Once);
        mockStatusReporter.Verify(r => r.ReportStatusAsync(IndexerStatus.Idle, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SearchTracksInternalAsync Exception Tests

    [Fact]
    public async Task SearchTracksInternalAsync_WhenException_ReportsErrorAndRethrows()
    {
        // Arrange
        var expectedException = new HttpRequestException("Track search failed");
        var core = new SearchThrowingCore(expectedException);
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatusReporter = new Mock<IIndexerStatusReporter>();

        var indexer = new TestableTidalIndexer(
            searchService, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatusReporter.Object);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => indexer.ExposeSearchTracksAsync("query"));
        Assert.Equal("Track search failed", thrown.Message);
        mockStatusReporter.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == expectedException), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetAlbumDetailsInternalAsync Tests

    [Fact]
    public async Task GetAlbumDetailsInternalAsync_WhenException_ReportsErrorAndRethrows()
    {
        // Arrange
        var expectedException = new HttpRequestException("Album fetch failed");
        var core = new AlbumThrowingCore(expectedException);
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatusReporter = new Mock<IIndexerStatusReporter>();

        var indexer = new TestableTidalIndexer(
            searchService, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatusReporter.Object);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => indexer.ExposeGetAlbumDetailsAsync("album1"));
        Assert.Equal("Album fetch failed", thrown.Message);
        mockStatusReporter.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == expectedException), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ValidateSettings Tests

    [Fact]
    public void ValidateSettings_WhenTidalMarketEmpty_ReturnsError()
    {
        // Arrange - settings with empty TidalMarket
        var settings = new TidalIndexerSettings
        {
            RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
            ConfigPath = Path.GetTempPath(),
            TidalMarket = "" // Empty market
        };
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TestableTidalIndexer(searchService, core, settings, NullLogger.Instance);

        // Act
        ValidationResult result = indexer.ExposeValidateSettings(settings);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TidalMarket");
    }

    [Fact]
    public void ValidateSettings_WhenConfigPathEmpty_ReturnsError()
    {
        // Arrange - settings with empty ConfigPath
        var settings = new TidalIndexerSettings
        {
            RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
            ConfigPath = "", // Empty config path
            TidalMarket = "US"
        };
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TestableTidalIndexer(searchService, core, settings, NullLogger.Instance);

        // Act
        ValidationResult result = indexer.ExposeValidateSettings(settings);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ConfigPath");
    }

    [Fact]
    public void ValidateSettings_WhenAllFieldsValid_ReturnsValid()
    {
        // Arrange
        var settings = ValidSettings;
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TestableTidalIndexer(searchService, core, settings, NullLogger.Instance);

        // Act
        ValidationResult result = indexer.ExposeValidateSettings(settings);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region ValidateSettingsWithDiagnostics Tests

    [Fact]
    public void ValidateSettingsWithDiagnostics_WhenValid_ReturnsSuccessWithOkId()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(searchService, core, ValidSettings, NullLogger.Instance);

        // Act
        var result = indexer.ValidateSettingsWithDiagnostics();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("IX000", result.Value!["id"]);
        Assert.Equal("Tidal", result.Value["service"]);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WhenInvalid_ReturnsFailureWithInvalidCode()
    {
        // Arrange
        var invalidSettings = new TidalIndexerSettings
        {
            RedirectUrl = "", // Invalid
            ConfigPath = "",  // Invalid
            TidalMarket = ""  // Invalid
        };
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(searchService, core, invalidSettings, NullLogger.Instance);

        // Act
        var result = indexer.ValidateSettingsWithDiagnostics();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Equal("IX100", result.Error.Metadata["id"]);
        Assert.Equal("Tidal", result.Error.Metadata["service"]);
    }

    #endregion

    #region InitializeWithDiagnosticsAsync Tests

    [Fact]
    public async Task InitializeWithDiagnosticsAsync_WhenValidAndAuthenticated_ReturnsSuccess()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(searchService, core, ValidSettings, NullLogger.Instance);

        // Act
        var result = await indexer.InitializeWithDiagnosticsAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("IX000", result.Value!["id"]);
        Assert.Equal("Tidal", result.Value["service"]);
    }

    [Fact]
    public async Task InitializeWithDiagnosticsAsync_WhenSettingsInvalid_ReturnsSettingsFailure()
    {
        // Arrange
        var invalidSettings = new TidalIndexerSettings
        {
            RedirectUrl = "",
            ConfigPath = "",
            TidalMarket = ""
        };
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(searchService, core, invalidSettings, NullLogger.Instance);

        // Act
        var result = await indexer.InitializeWithDiagnosticsAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Equal("IX100", result.Error.Metadata["id"]);
    }

    [Fact]
    public async Task InitializeWithDiagnosticsAsync_WhenNotAuthenticated_ReturnsAuthFailure()
    {
        // Arrange - Create a core that returns false for auth
        var unauthenticatedCore = new AuthenticatedCore();
        var coreField = typeof(AuthenticatedCore).GetField("Authenticated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        // Use reflection alternative - create a new implementation
        var mockCore = new Mock<ITidalCore>();
        mockCore.Setup(c => c.IsAuthenticatedAsync()).ReturnsAsync(false);
        mockCore.Setup(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TidalSearchResults([], [], [], 0, false));
        mockCore.Setup(c => c.GetAlbumAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));

        var searchService = new TidalSearchService(mockCore.Object, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(searchService, mockCore.Object, ValidSettings, NullLogger.Instance);

        // Act
        var result = await indexer.InitializeWithDiagnosticsAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginErrorCode.Unauthorized, result.Error!.Code);
        Assert.Equal("IX200", result.Error.Metadata["id"]);
        Assert.Equal("Tidal", result.Error.Metadata["service"]);
    }

    [Fact]
    public async Task InitializeWithDiagnosticsAsync_WhenAuthThrows_ReturnsAuthFailureWithException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Auth error");
        var core = new ThrowingCore(expectedException);
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(searchService, core, ValidSettings, NullLogger.Instance);

        // Act
        var result = await indexer.InitializeWithDiagnosticsAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginErrorCode.Unauthorized, result.Error!.Code);
        Assert.Equal("IX200", result.Error.Metadata["id"]);
        Assert.Equal("Auth error", result.Error.Message);
    }

    #endregion

    #region SearchEnhancedAsync Tests

    [Fact]
    public async Task SearchEnhancedAsync_WhenSuccess_ReturnsSearchResults()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(searchService, core, ValidSettings, NullLogger.Instance);

        // Act
        var results = await indexer.SearchEnhancedAsync("Daft Punk");

        // Assert
        Assert.NotEmpty(results);
        // Should contain both albums and tracks
        Assert.Contains(results, r => r.Type == Lidarr.Plugin.Abstractions.Models.StreamingSearchType.Album);
        Assert.Contains(results, r => r.Type == Lidarr.Plugin.Abstractions.Models.StreamingSearchType.Track);
    }

    [Fact]
    public async Task SearchEnhancedAsync_WhenException_ReportsErrorAndRethrows()
    {
        // Arrange
        var expectedException = new HttpRequestException("Enhanced search failed");
        var core = new SearchThrowingCore(expectedException);
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatusReporter = new Mock<IIndexerStatusReporter>();

        var indexer = new TidalIndexer(
            searchService, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatusReporter.Object);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => indexer.SearchEnhancedAsync("query"));
        Assert.Equal("Enhanced search failed", thrown.Message);
        mockStatusReporter.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == expectedException), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Constructor and GetHttpClient Tests

    [Fact]
    public void Constructor_WithoutTokenProvider_CreatesBasicHttpClient()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());

        // Act
        var indexer = new TestableTidalIndexer(searchService, core, ValidSettings, NullLogger.Instance, tokenProvider: null);

        // Assert
        var httpClient = indexer.ExposeGetHttpClient();
        Assert.NotNull(httpClient);
    }

    [Fact]
    public void Constructor_WithTokenProvider_CreatesOAuthHttpClient()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockTokenProvider = new Mock<Lidarr.Plugin.Common.Interfaces.IStreamingTokenProvider>();

        // Act
        var indexer = new TestableTidalIndexer(searchService, core, ValidSettings, NullLogger.Instance, tokenProvider: mockTokenProvider.Object);

        // Assert
        var httpClient = indexer.ExposeGetHttpClient();
        Assert.NotNull(httpClient);
        Assert.Equal(TimeSpan.FromSeconds(100), httpClient.Timeout);
    }

    [Fact]
    public void GetHttpClient_ReturnsConfiguredClient()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TestableTidalIndexer(searchService, core, ValidSettings, NullLogger.Instance);

        // Act
        var client = indexer.ExposeGetHttpClient();

        // Assert
        Assert.NotNull(client);
    }

    #endregion

    #region ServiceName and ProtocolName Tests

    [Fact]
    public void ServiceName_ReturnsTidal()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var searchService = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(searchService, core, ValidSettings, NullLogger.Instance);

        // Act & Assert - ServiceName is protected, verify via behavior
        var diagnostics = indexer.ValidateSettingsWithDiagnostics();
        Assert.Equal("Tidal", diagnostics.Value!["service"]);
    }

    #endregion
}
