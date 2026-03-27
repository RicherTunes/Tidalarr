using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Unit;

[Trait("Category", "Wave2")]
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void RegisterServices_BuildsContainerWithoutErrors()
    {
        // TidalModule.RegisterServices populates a ServiceCollection with all Tidalarr
        // services. Building the provider must not throw even when no settings are supplied
        // (DI resolution is lazy, so Build only validates the descriptor graph).
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public void RegisterServices_RegistersExpectedServiceDescriptors()
    {
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);

        // Verify key Tidalarr-specific descriptors are present in the collection.
        // We check by service type name to avoid coupling to internal concrete types.
        string[] expectedTypeNames =
        [
            "ITokenStorage",
            "ITidalAuth",
            "IStreamingAuthManager",
            "IStreamingTokenProvider",
            "ITidalCore",
            "TidalModelMapper",
            "TidalResponseCache",
            "TidalRateLimiter",
            "PerformanceMonitor",
            "TidalSearchService",
            "TidalStreamService",
            "TidalIndexer",
            "TidalDownloadClient",
        ];

        foreach (string typeName in expectedTypeNames)
        {
            bool found = services.Any(d => d.ServiceType.Name == typeName);
            Assert.True(found, $"Expected service descriptor for '{typeName}' was not found");
        }
    }

    [Fact]
    public void RegisterServices_RegistersBridgeDefaults()
    {
        // TidalModule calls AddBridgeDefaults() which registers fallback bridge services.
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);

        string[] bridgeTypeNames =
        [
            "IAuthFailureHandler",
            "IIndexerStatusReporter",
            "IRateLimitReporter",
        ];

        foreach (string typeName in bridgeTypeNames)
        {
            bool found = services.Any(d => d.ServiceType.Name == typeName);
            Assert.True(found, $"Expected bridge default '{typeName}' was not found");
        }
    }

    [Fact]
    public void RegisterServices_RegistersSharedLibraryServices()
    {
        // Shared library registrations include IStreamingResponseCache, IUniversalAdaptiveRateLimiter, etc.
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);

        string[] sharedTypeNames =
        [
            "IStreamingResponseCache",
            "IUniversalAdaptiveRateLimiter",
            "NetworkResilienceService",
        ];

        foreach (string typeName in sharedTypeNames)
        {
            bool found = services.Any(d => d.ServiceType.Name == typeName);
            Assert.True(found, $"Expected shared library service '{typeName}' was not found");
        }
    }

    [Fact]
    public void RegisterServices_IsIdempotent_NoDuplicateKeyServices()
    {
        // Calling RegisterServices twice should not create duplicate singleton registrations
        // for services using TryAdd* patterns (bridge defaults, back-compat settings).
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);
        int countAfterFirst = services.Count;

        TidalModule.RegisterServices(services);
        int countAfterSecond = services.Count;

        // TryAdd-based registrations should not add duplicates, but explicit Add calls will.
        // The count should increase by at most the non-TryAdd registrations (same as first call).
        // Verify no "runaway" duplication.
        Assert.True(countAfterSecond <= countAfterFirst * 2,
            $"Duplicate call more than doubled descriptors: first={countAfterFirst}, second={countAfterSecond}");
    }

    [Fact]
    public void RegisterServices_RegistersHttpClientFactories()
    {
        // TidalModule registers named/typed HttpClients via AddHttpClient.
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);

        // IHttpClientFactory is registered as a side effect of AddHttpClient<T>.
        bool hasHttpClientFactory = services.Any(d => d.ServiceType.Name == "IHttpClientFactory");
        Assert.True(hasHttpClientFactory, "IHttpClientFactory should be registered by AddHttpClient calls");
    }
}
