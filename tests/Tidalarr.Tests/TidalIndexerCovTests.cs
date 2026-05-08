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
/// Additional coverage tests for TidalIndexer covering:
/// - AuthenticateAsync with IAuthFailureHandler success/failure callbacks
/// - IIndexerStatusReporter status transitions during operations
/// - Exception propagation with status reporting
/// - GetHttpClient with/without token provider
/// - ValidateSettings direct validation
/// - ValidateSettingsWithDiagnostics and InitializeWithDiagnosticsAsync
/// - SearchEnhancedAsync with status reporting
/// </summary>
public class TidalIndexerCovTests
{
    private static TidalIndexerSettings ValidSettings => new()
    {
        RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
        ConfigPath = Path.GetTempPath(),
        TidalMarket = "US"
    };

    #region ITidalCore Implementations

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

    private class UnauthenticatedCore : ITidalCore
    {
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

        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(false);
    }

    private class AuthThrowingCore : ITidalCore
    {
        private readonly Exception _exception;

        public AuthThrowingCore(Exception exception) => _exception = exception;

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

        public Task<bool> IsAuthenticatedAsync() => throw _exception;
    }

    private class SearchThrowingCore : ITidalCore
    {
        private readonly Exception _exception;

        public SearchThrowingCore(Exception exception) => _exception = exception;

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));

        public Task<List<TidalTrackInfo>> GetAlbumTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TidalTrackInfo>());

        public Task<TidalAlbumInfo> GetAlbumWithTracksAsync(string albumId, CancellationToken cancellationToken = default)
            => GetAlbumAsync(albumId, cancellationToken);

        public Task<TidalSearchResults> SearchAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
            => throw _exception;

        public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalStreamInfo(trackId, [], ".flac", "audio/flac", false, null));

        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(true);
    }

    private class AlbumThrowingCore : ITidalCore
    {
        private readonly Exception _exception;

        public AlbumThrowingCore(Exception exception) => _exception = exception;

        public Task<TidalTrackInfo> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TidalTrackInfo("", "", [], "", "", 0, 0, TidalQuality.High, true, DateTime.MinValue));

        public Task<TidalAlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
            => throw _exception;

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

    #endregion

    #region Testable TidalIndexer

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
            : base(searchService, apiClient, settings, logger!, tokenProvider, authHandler, statusReporter)
        {
        }

        public Task<bool> ExposeAuthenticateAsync() => AuthenticateAsync();
        public Task<List<Lidarr.Plugin.Abstractions.Models.StreamingAlbum>> ExposeSearchAlbumsAsync(string term) => SearchAlbumsAsync(term);
        public Task<List<Lidarr.Plugin.Abstractions.Models.StreamingTrack>> ExposeSearchTracksAsync(string term) => SearchTracksAsync(term);
        public Task<Lidarr.Plugin.Abstractions.Models.StreamingAlbum> ExposeGetAlbumDetailsAsync(string id) => GetAlbumDetailsAsync(id);
        public ValidationResult ExposeValidateSettings(TidalIndexerSettings s) => ValidateSettings(s);
        public HttpClient ExposeGetHttpClient() => GetHttpClient();
    }

    #endregion

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_WhenAuthenticated_ReportsAuthenticatingAndCallsSuccessHandler()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockAuth = new Mock<IAuthFailureHandler>();
        var mockStatus = new Mock<IIndexerStatusReporter>();
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance,
            authHandler: mockAuth.Object, statusReporter: mockStatus.Object);

        // Act
        bool result = await indexer.ExposeAuthenticateAsync();

        // Assert
        Assert.True(result);
        mockStatus.Verify(r => r.ReportStatusAsync(IndexerStatus.Authenticating, null, It.IsAny<CancellationToken>()), Times.Once);
        mockAuth.Verify(h => h.HandleSuccessAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenException_ReportsErrorAndCallsFailureHandler()
    {
        // Arrange
        var ex = new InvalidOperationException("Auth error");
        var core = new AuthThrowingCore(ex);
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockAuth = new Mock<IAuthFailureHandler>();
        var mockStatus = new Mock<IIndexerStatusReporter>();
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance,
            authHandler: mockAuth.Object, statusReporter: mockStatus.Object);

        // Act
        bool result = await indexer.ExposeAuthenticateAsync();

        // Assert
        Assert.False(result);
        mockStatus.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == ex), It.IsAny<CancellationToken>()), Times.Once);
        mockAuth.Verify(h => h.HandleFailureAsync(
            It.Is<AuthFailure>(f => f.ErrorCode == "TIDAL_AUTH" && f.CanReauthenticate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutHandlers_ReturnsTrueWhenAuthenticated()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance);

        // Act
        bool result = await indexer.ExposeAuthenticateAsync();

        // Assert
        Assert.True(result);
    }

    #endregion

    #region SearchAlbumsAsync Tests

    [Fact]
    public async Task SearchAlbumsAsync_WithStatusReporter_ReportsSearchingAndIdle()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatus = new Mock<IIndexerStatusReporter>();
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatus.Object);

        // Act
        var albums = await indexer.ExposeSearchAlbumsAsync("query");

        // Assert
        Assert.NotEmpty(albums);
        mockStatus.Verify(r => r.ReportStatusAsync(IndexerStatus.Searching, "query", It.IsAny<CancellationToken>()), Times.Once);
        mockStatus.Verify(r => r.ReportStatusAsync(IndexerStatus.Idle, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAlbumsAsync_WhenException_ReportsErrorAndRethrows()
    {
        // Arrange
        var ex = new HttpRequestException("Network failure");
        var core = new SearchThrowingCore(ex);
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatus = new Mock<IIndexerStatusReporter>();
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatus.Object);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => indexer.ExposeSearchAlbumsAsync("q"));
        Assert.Equal("Network failure", thrown.Message);
        mockStatus.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == ex), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SearchTracksAsync Tests

    [Fact]
    public async Task SearchTracksAsync_WhenException_ReportsErrorAndRethrows()
    {
        // Arrange
        var ex = new HttpRequestException("Track search failed");
        var core = new SearchThrowingCore(ex);
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatus = new Mock<IIndexerStatusReporter>();
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatus.Object);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => indexer.ExposeSearchTracksAsync("q"));
        Assert.Equal("Track search failed", thrown.Message);
        mockStatus.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == ex), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetAlbumDetailsAsync Tests

    [Fact]
    public async Task GetAlbumDetailsAsync_WhenException_ReportsErrorAndRethrows()
    {
        // Arrange
        var ex = new HttpRequestException("Album fetch failed");
        var core = new AlbumThrowingCore(ex);
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatus = new Mock<IIndexerStatusReporter>();
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance,
            statusReporter: mockStatus.Object);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => indexer.ExposeGetAlbumDetailsAsync("album1"));
        Assert.Equal("Album fetch failed", thrown.Message);
        mockStatus.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == ex), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ValidateSettings Tests

    [Fact]
    public void ValidateSettings_WhenTidalMarketEmpty_ReturnsValidationError()
    {
        // Arrange
        var settings = new TidalIndexerSettings
        {
            RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
            ConfigPath = Path.GetTempPath(),
            TidalMarket = ""
        };
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TestableTidalIndexer(search, core, settings, NullLogger.Instance);

        // Act
        ValidationResult result = indexer.ExposeValidateSettings(settings);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TidalMarket");
    }

    [Fact]
    public void ValidateSettings_WhenConfigPathEmpty_ReturnsValidationError()
    {
        // Arrange
        var settings = new TidalIndexerSettings
        {
            RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y",
            ConfigPath = "",
            TidalMarket = "US"
        };
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TestableTidalIndexer(search, core, settings, NullLogger.Instance);

        // Act
        ValidationResult result = indexer.ExposeValidateSettings(settings);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ConfigPath");
    }

    [Fact]
    public void ValidateSettings_WhenAllValid_ReturnsValid()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance);

        // Act
        ValidationResult result = indexer.ExposeValidateSettings(ValidSettings);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region ValidateSettingsWithDiagnostics Tests

    [Fact]
    public void ValidateSettingsWithDiagnostics_WhenValid_ReturnsSuccessWithCodeIX000()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(search, core, ValidSettings, NullLogger.Instance);

        // Act
        var result = indexer.ValidateSettingsWithDiagnostics();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("IX000", result.Value!["id"]);
        Assert.Equal("Tidal", result.Value["service"]);
    }

    [Fact]
    public void ValidateSettingsWithDiagnostics_WhenInvalid_ReturnsFailureWithCodeIX100()
    {
        // Arrange
        var invalid = new TidalIndexerSettings { RedirectUrl = "", ConfigPath = "", TidalMarket = "" };
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(search, core, invalid, NullLogger.Instance);

        // Act
        var result = indexer.ValidateSettingsWithDiagnostics();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Equal("IX100", result.Error.Metadata["id"]);
    }

    #endregion

    #region InitializeWithDiagnosticsAsync Tests

    [Fact]
    public async Task InitializeWithDiagnosticsAsync_WhenValidAndAuthenticated_ReturnsSuccess()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(search, core, ValidSettings, NullLogger.Instance);

        // Act
        var result = await indexer.InitializeWithDiagnosticsAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("IX000", result.Value!["id"]);
    }

    [Fact]
    public async Task InitializeWithDiagnosticsAsync_WhenSettingsInvalid_ReturnsIX100()
    {
        // Arrange
        var invalid = new TidalIndexerSettings { RedirectUrl = "", ConfigPath = "", TidalMarket = "" };
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(search, core, invalid, NullLogger.Instance);

        // Act
        var result = await indexer.InitializeWithDiagnosticsAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("IX100", result.Error!.Metadata["id"]);
    }

    [Fact]
    public async Task InitializeWithDiagnosticsAsync_WhenNotAuthenticated_ReturnsIX200()
    {
        // Arrange
        var mockCore = new Mock<ITidalCore>();
        mockCore.Setup(c => c.IsAuthenticatedAsync()).ReturnsAsync(false);
        mockCore.Setup(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TidalSearchResults([], [], [], 0, false));
        mockCore.Setup(c => c.GetAlbumAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TidalAlbumInfo("", "", [], [], [], DateTime.MinValue, "", true));
        var search = new TidalSearchService(mockCore.Object, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(search, mockCore.Object, ValidSettings, NullLogger.Instance);

        // Act
        var result = await indexer.InitializeWithDiagnosticsAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginErrorCode.Unauthorized, result.Error!.Code);
        Assert.Equal("IX200", result.Error.Metadata["id"]);
    }

    [Fact]
    public async Task InitializeWithDiagnosticsAsync_WhenAuthThrows_ReturnsIX200WithMessage()
    {
        // Arrange
        var ex = new InvalidOperationException("Auth error");
        var core = new AuthThrowingCore(ex);
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(search, core, ValidSettings, NullLogger.Instance);

        // Act
        var result = await indexer.InitializeWithDiagnosticsAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("IX200", result.Error!.Metadata["id"]);
        Assert.Equal("Auth error", result.Error.Message);
    }

    #endregion

    #region SearchEnhancedAsync Tests

    [Fact]
    public async Task SearchEnhancedAsync_ReturnsAlbumsAndTracks()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var indexer = new TidalIndexer(search, core, ValidSettings, NullLogger.Instance);

        // Act
        var results = await indexer.SearchEnhancedAsync("query");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Type == Lidarr.Plugin.Abstractions.Models.StreamingSearchType.Album);
        Assert.Contains(results, r => r.Type == Lidarr.Plugin.Abstractions.Models.StreamingSearchType.Track);
    }

    [Fact]
    public async Task SearchEnhancedAsync_WhenException_ReportsErrorAndRethrows()
    {
        // Arrange
        var ex = new HttpRequestException("Enhanced search failed");
        var core = new SearchThrowingCore(ex);
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockStatus = new Mock<IIndexerStatusReporter>();
        var indexer = new TidalIndexer(search, core, ValidSettings, NullLogger.Instance, statusReporter: mockStatus.Object);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => indexer.SearchEnhancedAsync("q"));
        Assert.Equal("Enhanced search failed", thrown.Message);
        mockStatus.Verify(r => r.ReportErrorAsync(It.Is<Exception>(e => e == ex), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Constructor/GetHttpClient Tests

    [Fact]
    public void Constructor_WithoutTokenProvider_CreatesHttpClient()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());

        // Act
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance, tokenProvider: null);

        // Assert
        Assert.NotNull(indexer.ExposeGetHttpClient());
    }

    [Fact]
    public void Constructor_WithTokenProvider_CreatesHttpClientWithTimeout()
    {
        // Arrange
        var core = new AuthenticatedCore();
        var search = new TidalSearchService(core, new Domain.Quality.TidalQualityDetector());
        var mockToken = new Mock<Lidarr.Plugin.Common.Interfaces.IStreamingTokenProvider>();

        // Act
        var indexer = new TestableTidalIndexer(search, core, ValidSettings, NullLogger.Instance, tokenProvider: mockToken.Object);

        // Assert
        var client = indexer.ExposeGetHttpClient();
        Assert.NotNull(client);
        Assert.Equal(TimeSpan.FromSeconds(100), client.Timeout);
    }

    #endregion
}
