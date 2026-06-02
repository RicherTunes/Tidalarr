using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Intelligence;
using Lidarr.Plugin.Common.Services.Performance;
using Tidalarr.Application.Services;
using Tidalarr.Core.Interfaces;
using Tidalarr.Domain.Api;
using Tidalarr.Domain.Authentication;
using Tidalarr.Infrastructure.Caching;
using Tidalarr.Infrastructure.Performance;
using Tidalarr.Integration;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests;

public class TidalModuleDiTests
{
    [Fact]
    public void RegistersExpectedServices_AndLifetimes()
    {
        ServiceCollection services = new();
        TidalIndexerSettings indexerSettings = new() { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        TidalDownloadClientSettings downloadSettings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        _ = services.AddSingleton(indexerSettings);
        _ = services.AddSingleton(downloadSettings);
        TidalModule.RegisterServices(services);

        ServiceProvider provider = services.BuildServiceProvider();

        // API typed client
        Assert.NotNull(provider.GetRequiredService<TidalApiClient>());

        // Interfaces
        _ = Assert.IsType<TidalOAuthService>(provider.GetRequiredService<ITidalAuth>());
        _ = Assert.IsType<TidalApiClient>(provider.GetRequiredService<ITidalCore>());
        _ = Assert.IsType<TidalResponseCache>(provider.GetRequiredService<IStreamingResponseCache>());

        // Integration endpoints
        Assert.NotNull(provider.GetRequiredService<TidalIndexer>());
        Assert.NotNull(provider.GetRequiredService<TidalDownloadClient>());

        // Performance services
        IUniversalAdaptiveRateLimiter limiter = provider.GetRequiredService<IUniversalAdaptiveRateLimiter>();
        _ = Assert.IsType<TidalRateLimiter>(limiter);
    }

    // ---- IQueryOptimizer consumer-switch (Common #611 HeuristicQueryOptimizer) ----
    //
    // TidalSearchService takes an optional IQueryOptimizer ctor parameter and, when
    // present, drives an optimize -> search -> learn loop. Historically nothing
    // registered IQueryOptimizer in DI, so the parameter resolved to null and the
    // whole feedback loop was dead-wired. These tests pin that the wire is now live
    // via Common's dependency-free HeuristicQueryOptimizer, AND assert the safety
    // invariant that makes lighting it up non-regressing: the PRIMARY optimized
    // query is the (whitespace-normalized) raw query — never a term-dropped rewrite.
    // Recall-adding rewrites live only in OptimizedQuery.Alternatives, which the
    // consumer does not (yet) fan out into, so the user-visible result path is
    // unchanged.

    [Fact]
    public void RegistersHeuristicQueryOptimizer_AsIQueryOptimizer()
    {
        ServiceProvider provider = BuildProvider();

        IQueryOptimizer optimizer = provider.GetRequiredService<IQueryOptimizer>();
        _ = Assert.IsType<HeuristicQueryOptimizer>(optimizer);
    }

    [Fact]
    public void TidalSearchService_ResolvesWithLiveOptimizer()
    {
        ServiceProvider provider = BuildProvider();

        // The search service must resolve, and the (formerly dead-wired) optimizer
        // dependency must now be satisfiable from the same container.
        Assert.NotNull(provider.GetRequiredService<TidalSearchService>());
        Assert.NotNull(provider.GetService<IQueryOptimizer>());
    }

    [Fact]
    public async Task RegisteredOptimizer_PrimaryQuery_PreservesEssentialQuery_NoTermDropping()
    {
        ServiceProvider provider = BuildProvider();
        IQueryOptimizer optimizer = provider.GetRequiredService<IQueryOptimizer>();

        // A decorated query that the heuristic engine WOULD rewrite into alternatives
        // (edition-strip, featured-artist-drop). The safety invariant is that the
        // PRIMARY query still carries every essential token of the raw input — the
        // engine only collapses whitespace on the primary path.
        const string raw = "Daft  Punk   Discovery (Remastered) feat. Pharrell";
        OptimizedQuery result = await optimizer.OptimizeQueryAsync(raw, new QueryContext { Type = QueryType.Album });

        // Primary == whitespace-normalized raw query (no terms removed).
        string normalizedRaw = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ").Trim();
        Assert.Equal(normalizedRaw, result.Query);

        // Every essential token from the raw query survives on the primary path.
        foreach (string token in new[] { "Daft", "Punk", "Discovery", "Remastered", "Pharrell" })
        {
            Assert.Contains(token, result.Query, StringComparison.OrdinalIgnoreCase);
        }

        // Recall-adding rewrites are additive: any alternatives are distinct from
        // the primary (they never replace it).
        Assert.DoesNotContain(result.Query, result.Alternatives);
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        TidalIndexerSettings indexerSettings = new() { RedirectUrl = "https://tidal.com/android/login/auth?code=x&state=y", ConfigPath = Path.GetTempPath() };
        TidalDownloadClientSettings downloadSettings = new() { PreferredQuality = TidalQuality.Lossless, DownloadPath = Path.GetTempPath() };
        _ = services.AddSingleton(indexerSettings);
        _ = services.AddSingleton(downloadSettings);
        TidalModule.RegisterServices(services);
        return services.BuildServiceProvider();
    }
}


