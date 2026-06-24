using Lidarr.Plugin.Common.Services.Bridge;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Integration;

namespace Tidalarr.Tests.Resilience;

/// <summary>
/// Verifies Tidalarr adopts <see cref="AuthFailureGate"/> per the apple/qobuz baseline:
/// the gate is registered as a singleton wrapping the bridge-default
/// <see cref="Lidarr.Plugin.Abstractions.Contracts.IAuthFailureHandler"/> so the indexer +
/// download client + auth service all share one latch state.
///
/// Background: prior to this adoption, Tidalarr had a comment-only reference at
/// <c>TidalModule.cs:59</c> ("It is independent of AuthFailureGate") but never wired
/// the gate. Lidarr's search loop hammering Tidal at full rate on a dead session is
/// the qobuzarr-incident class — fixed by adopting Common's gate, matching apple +
/// qobuz which already register a singleton AuthFailureGate.
///
/// Test methodology: these tests inspect the <see cref="IServiceCollection"/> by type
/// FullName rather than calling <c>BuildServiceProvider().GetRequiredService&lt;T&gt;()</c>
/// because Tidalarr.dll is ILRepacked — types from <c>Lidarr.Plugin.Common</c> and
/// <c>Lidarr.Plugin.Abstractions</c> are internalized into the merged plugin DLL with
/// the same FQN but a different assembly identity than the standalone copies the test
/// project references. Resolving by <c>typeof(T)</c> would compare assembly identity
/// and miss the registration. The FullName inspection is identity-agnostic.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public class AuthFailureGateAdoptionTests
{
    private const string AuthFailureGateFullName = "Lidarr.Plugin.Common.Services.Bridge.AuthFailureGate";
    private const string IAuthFailureHandlerFullName = "Lidarr.Plugin.Abstractions.Contracts.IAuthFailureHandler";

    private static IServiceCollection BuildTidalServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        TidalModule.RegisterServices(services);
        return services;
    }

    private static void SkipIfHostBridgeExcluded()
    {
#if EXCLUDE_HOST_BRIDGE
        Skip.If(true, "Host bridge types are excluded in hostless CI.");
#endif
    }

    [Fact]
    public void TidalModule_RegistersAuthFailureGate_AsSingleton()
    {
        var services = BuildTidalServices();

        var registration = services.FirstOrDefault(
            s => s.ServiceType.FullName == AuthFailureGateFullName);

        Assert.NotNull(registration);
        Assert.Equal(ServiceLifetime.Singleton, registration!.Lifetime);
    }

    [Fact]
    public void TidalModule_RegistersExactlyOneAuthFailureGate()
    {
        // Defends against accidentally registering two gate instances (which would
        // defeat the "single shared latch" guarantee — indexer's 401 wouldn't gate
        // the download client). Exactly one registration is the contract.
        var services = BuildTidalServices();

        var gateRegistrations = services
            .Where(s => s.ServiceType.FullName == AuthFailureGateFullName)
            .ToList();

        Assert.Single(gateRegistrations);
    }

    [Fact]
    public void TidalModule_RegistersBridgeDefaultAuthFailureHandler()
    {
        // The gate depends on IAuthFailureHandler being registered by
        // services.AddBridgeDefaults(). Without it the gate's factory would
        // throw at first resolution. Verify the dependency is in place.
        var services = BuildTidalServices();

        var handlerReg = services.FirstOrDefault(
            s => s.ServiceType.FullName == IAuthFailureHandlerFullName);

        Assert.NotNull(handlerReg);
        Assert.Equal(ServiceLifetime.Singleton, handlerReg!.Lifetime);
    }

    // ------------------------------------------------------------------ //
    // Wiring tests: verify the indexer and download client define the
    // private static helpers (IsAuthShortCircuited + RecordAuthOutcomeFromException
    // + LooksLikeAuthFailure) that mirror apple's AppleMusicIndexerAdapter
    // pattern. Reflection-based because the helpers are private; the call
    // sites in FetchReleases / Test / Download invoke them by name.
    //
    // These tests catch the regression class where a future refactor removes
    // the helpers or the call sites without removing the corresponding entry
    // in CLAUDE.md "Common helpers in use".
    // ------------------------------------------------------------------ //

    [SkippableFact]
    public void TidalLidarrIndexer_DefinesAuthShortCircuitHelper()
    {
        SkipIfHostBridgeExcluded();

        var type = Type.GetType(
            "Tidalarr.Integration.LidarrNative.TidalLidarrIndexer, Lidarr.Plugin.Tidalarr",
            throwOnError: false);
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "IsAuthShortCircuited",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method!.ReturnType);
    }

    [SkippableFact]
    public void TidalLidarrIndexer_DefinesRecordAuthOutcomeHelper()
    {
        SkipIfHostBridgeExcluded();

        var type = Type.GetType(
            "Tidalarr.Integration.LidarrNative.TidalLidarrIndexer, Lidarr.Plugin.Tidalarr",
            throwOnError: false);
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "RecordAuthOutcomeFromException",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method!.ReturnType);
    }

    [SkippableFact]
    public void TidalLidarrDownloadClient_DefinesAuthShortCircuitHelper()
    {
        SkipIfHostBridgeExcluded();

        var type = Type.GetType(
            "Tidalarr.Integration.LidarrNative.TidalLidarrDownloadClient, Lidarr.Plugin.Tidalarr",
            throwOnError: false);
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "IsAuthShortCircuited",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method!.ReturnType);
    }

    [SkippableFact]
    public void TidalLidarrDownloadClient_DefinesRecordAuthOutcomeHelper()
    {
        SkipIfHostBridgeExcluded();

        var type = Type.GetType(
            "Tidalarr.Integration.LidarrNative.TidalLidarrDownloadClient, Lidarr.Plugin.Tidalarr",
            throwOnError: false);
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "RecordAuthOutcomeFromException",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method!.ReturnType);
    }

    [Fact]
    public void TidalModule_AuthFailureGate_RegisteredBeforeBuildServiceProvider()
    {
        // Sanity check: the registration ordering allows resolution. We can't
        // call GetRequiredService<AuthFailureGate>() across ALCs, but we can
        // verify the service descriptor has a factory (or implementation) and
        // doesn't trip the trivial "no descriptor" path that BuildServiceProvider
        // would catch.
        var services = BuildTidalServices();

        var registration = services
            .First(s => s.ServiceType.FullName == AuthFailureGateFullName);

        // Singleton-with-factory pattern (matches apple's wiring): ImplementationFactory
        // is set so the gate can pull IAuthFailureHandler + ILogger from the provider.
        bool hasFactory = registration.ImplementationFactory is not null;
        bool hasType = registration.ImplementationType is not null;
        bool hasInstance = registration.ImplementationInstance is not null;

        Assert.True(hasFactory || hasType || hasInstance,
            "AuthFailureGate registration must have a factory, type, or instance.");
    }
}
