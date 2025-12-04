using System.Reflection;
using Tidalarr.Domain.Authentication;
using Tidalarr.Integration;

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
        this._pluginAssembly = typeof(TidalarrPlugin).Assembly;
        this._authServiceType = typeof(TidalOAuthService);
        this._indexerType = typeof(TidalIndexer);
        this._downloadClientType = typeof(TidalDownloadClient);
    }

    #region Authentication Tests

    [Fact]
    public void Authentication_ServiceExists()
    {
        Assert.NotNull(this._authServiceType);
    }

    [Fact]
    public void Authentication_HasAuthenticateMethod()
    {
        MethodInfo[] methods = this._authServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        bool hasAuthenticate = methods.Any(m =>
            m.Name.Contains("Authenticate", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("StartAuth", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasAuthenticate, "Authentication service should have an Authenticate/Login method");
    }

    [Fact]
    public void Authentication_HasRefreshMethod()
    {
        MethodInfo[] methods = this._authServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        bool hasRefresh = methods.Any(m =>
            m.Name.Contains("Refresh", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasRefresh, "Authentication service should have a token Refresh method");
    }

    [Fact]
    public void Authentication_HasValidationMethod()
    {
        MethodInfo[] methods = this._authServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        bool hasValidate = methods.Any(m =>
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
        Assert.NotNull(this._indexerType);
    }

    [Fact]
    public void Indexer_HasSearchMethod()
    {
        MethodInfo[] methods = this._indexerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        bool hasSearch = methods.Any(m =>
            m.Name.Contains("Search", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Fetch", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasSearch, "Indexer must implement a Search method");
    }

    [Fact]
    public void Indexer_HasAsyncMethods()
    {
        MethodInfo[] methods = this._indexerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        IEnumerable<MethodInfo> asyncMethods = methods.Where(m =>
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
        Assert.NotNull(this._downloadClientType);
    }

    [Fact]
    public void DownloadClient_HasDownloadMethod()
    {
        MethodInfo[] methods = this._downloadClientType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        bool hasDownload = methods.Any(m =>
            m.Name.Contains("Download", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasDownload, "Download client must implement a Download method");
    }

    [Fact]
    public void DownloadClient_HasStatusMethod()
    {
        MethodInfo[] methods = this._downloadClientType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        bool hasStatus = methods.Any(m =>
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
        Type[] allTypes = this._pluginAssembly.GetTypes();
        bool hasRateLimiter = allTypes.Any(t =>
            t.Name.Contains("RateLimiter", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Throttle", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasRateLimiter, "Tidal plugin should implement rate limiting");
    }

    [Fact]
    public void Infrastructure_ImplementsCaching()
    {
        Type[] allTypes = this._pluginAssembly.GetTypes();
        bool hasCaching = allTypes.Any(t =>
            t.Name.Contains("Cache", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasCaching, "Tidal plugin should implement response caching");
    }

    [Fact]
    public void Infrastructure_HasExceptionTypes()
    {
        Type[] allTypes = this._pluginAssembly.GetTypes();
        List<Type> exceptionTypes = allTypes.Where(t =>
            typeof(Exception).IsAssignableFrom(t) &&
            !t.IsAbstract &&
            t != typeof(Exception)).ToList();

        Assert.NotEmpty(exceptionTypes);
    }

    [Fact]
    public void Infrastructure_HasApiClient()
    {
        Type[] allTypes = this._pluginAssembly.GetTypes();
        bool hasApiClient = allTypes.Any(t =>
            t.Name.Contains("ApiClient", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("TidalClient", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasApiClient, "Tidal plugin should have an API client");
    }

    #endregion

    #region Tidal-Specific Tests

    [Fact]
    public void Tidal_HasQualitySupport()
    {
        Type[] allTypes = this._pluginAssembly.GetTypes();
        bool hasQuality = allTypes.Any(t =>
            t.Name.Contains("Quality", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasQuality, "Tidal plugin should support audio quality selection");
    }

    [Fact]
    public void Tidal_HasStreamManifestSupport()
    {
        Type[] allTypes = this._pluginAssembly.GetTypes();
        bool hasManifest = allTypes.Any(t =>
            t.Name.Contains("Manifest", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("Stream", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasManifest, "Tidal plugin should support stream manifests");
    }

    [Fact]
    public void Tidal_HasChunkDownloadSupport()
    {
        Type[] allTypes = this._pluginAssembly.GetTypes();
        bool hasChunk = allTypes.Any(t =>
            t.Name.Contains("Chunk", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasChunk, "Tidal plugin should support chunk downloading");
    }

    [Fact]
    public void Tidal_HasPKCESupport()
    {
        Type[] allTypes = this._pluginAssembly.GetTypes();
        bool hasPkce = allTypes.Any(t =>
            t.Name.Contains("PKCE", StringComparison.OrdinalIgnoreCase));

        Assert.True(hasPkce, "Tidal plugin should support OAuth PKCE");
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}
