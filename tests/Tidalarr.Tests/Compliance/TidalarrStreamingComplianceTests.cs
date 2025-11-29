using System;
using System.Linq;
using System.Reflection;
using Tidalarr.Domain.Authentication;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Compliance;

/// <summary>
/// Streaming service compliance tests for Tidalarr.
/// These tests verify Tidalarr implements all required streaming service patterns.
/// </summary>
[Trait("Category", "Compliance")]
[Trait("Category", "Streaming")]
public class TidalarrStreamingComplianceTests : IDisposable
{
    private readonly Assembly _pluginAssembly;
    private readonly Type _authServiceType;
    private readonly Type _indexerType;
    private readonly Type _downloadClientType;

    public TidalarrStreamingComplianceTests()
    {
        _pluginAssembly = typeof(TidalarrPlugin).Assembly;
        _authServiceType = typeof(TidalOAuthService);
        _indexerType = typeof(TidalIndexer);
        _downloadClientType = typeof(TidalDownloadClient);
    }

    #region Authentication Tests

    [Fact]
    public void Authentication_ServiceExists()
    {
        Assert.NotNull(_authServiceType);
    }

    [Fact]
    public void Authentication_HasAuthenticateMethod()
    {
        var methods = _authServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasAuthenticate = methods.Any(m =>
            m.Name.Contains("Authenticate", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("StartAuth", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasAuthenticate, "Authentication service should have an Authenticate/Login method");
    }

    [Fact]
    public void Authentication_HasRefreshMethod()
    {
        var methods = _authServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasRefresh = methods.Any(m =>
            m.Name.Contains("Refresh", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasRefresh, "Authentication service should have a token Refresh method");
    }

    [Fact]
    public void Authentication_HasValidationMethod()
    {
        var methods = _authServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasValidate = methods.Any(m =>
            m.Name.Contains("Validate", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("IsAuthenticated", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Check", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasValidate, "Authentication service should have a validation method");
    }

    #endregion

    #region Indexer Tests

    [Fact]
    public void Indexer_Exists()
    {
        Assert.NotNull(_indexerType);
    }

    [Fact]
    public void Indexer_HasSearchMethod()
    {
        var methods = _indexerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasSearch = methods.Any(m =>
            m.Name.Contains("Search", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Fetch", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasSearch, "Indexer must implement a Search method");
    }

    [Fact]
    public void Indexer_HasAsyncMethods()
    {
        var methods = _indexerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var asyncMethods = methods.Where(m =>
            m.ReturnType.IsGenericType &&
            (m.ReturnType.GetGenericTypeDefinition().Name.Contains("Task") ||
             m.ReturnType.GetGenericTypeDefinition().Name.Contains("ValueTask")));

        Assert.NotEmpty(asyncMethods);
    }

    #endregion

    #region Download Client Tests

    [Fact]
    public void DownloadClient_Exists()
    {
        Assert.NotNull(_downloadClientType);
    }

    [Fact]
    public void DownloadClient_HasDownloadMethod()
    {
        var methods = _downloadClientType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasDownload = methods.Any(m =>
            m.Name.Contains("Download", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasDownload, "Download client must implement a Download method");
    }

    [Fact]
    public void DownloadClient_HasStatusMethod()
    {
        var methods = _downloadClientType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var hasStatus = methods.Any(m =>
            m.Name.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("GetItems", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Queue", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasStatus, "Download client should implement a status/GetItems method");
    }

    #endregion

    #region Infrastructure Tests

    [Fact]
    public void Infrastructure_ImplementsRateLimiting()
    {
        var allTypes = _pluginAssembly.GetTypes();
        var hasRateLimiter = allTypes.Any(t =>
            t.Name.Contains("RateLimiter", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Throttle", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasRateLimiter, "Tidal plugin should implement rate limiting");
    }

    [Fact]
    public void Infrastructure_ImplementsCaching()
    {
        var allTypes = _pluginAssembly.GetTypes();
        var hasCaching = allTypes.Any(t =>
            t.Name.Contains("Cache", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasCaching, "Tidal plugin should implement response caching");
    }

    [Fact]
    public void Infrastructure_HasExceptionTypes()
    {
        var allTypes = _pluginAssembly.GetTypes();
        var exceptionTypes = allTypes.Where(t =>
            typeof(Exception).IsAssignableFrom(t) &&
            !t.IsAbstract &&
            t != typeof(Exception)).ToList();

        Assert.NotEmpty(exceptionTypes);
    }

    [Fact]
    public void Infrastructure_HasApiClient()
    {
        var allTypes = _pluginAssembly.GetTypes();
        var hasApiClient = allTypes.Any(t =>
            t.Name.Contains("ApiClient", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("TidalClient", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasApiClient, "Tidal plugin should have an API client");
    }

    #endregion

    #region Tidal-Specific Tests

    [Fact]
    public void Tidal_HasQualitySupport()
    {
        var allTypes = _pluginAssembly.GetTypes();
        var hasQuality = allTypes.Any(t =>
            t.Name.Contains("Quality", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasQuality, "Tidal plugin should support audio quality selection");
    }

    [Fact]
    public void Tidal_HasStreamManifestSupport()
    {
        var allTypes = _pluginAssembly.GetTypes();
        var hasManifest = allTypes.Any(t =>
            t.Name.Contains("Manifest", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Stream", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasManifest, "Tidal plugin should support stream manifests");
    }

    [Fact]
    public void Tidal_HasChunkDownloadSupport()
    {
        var allTypes = _pluginAssembly.GetTypes();
        var hasChunk = allTypes.Any(t =>
            t.Name.Contains("Chunk", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasChunk, "Tidal plugin should support chunk downloading");
    }

    [Fact]
    public void Tidal_HasPKCESupport()
    {
        var allTypes = _pluginAssembly.GetTypes();
        var hasPkce = allTypes.Any(t =>
            t.Name.Contains("PKCE", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasPkce, "Tidal plugin should support OAuth PKCE");
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}
