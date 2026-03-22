using Lidarr.Plugin.Abstractions.Contracts;
using Lidarr.Plugin.Common.Extensions;
using Lidarr.Plugin.Common.Services.Bridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tidalarr.Tests.Bridge;

/// <summary>
/// Proves that the bridge infrastructure resolves correctly when wired
/// the same way TidalModule does (AddBridgeDefaults at end of registration).
/// These tests don't call TidalModule.RegisterServices directly because that
/// loads FluentValidation transitively, which requires host assemblies.
/// </summary>
public class TidalBridgeIntegrationTests
{
    private static ServiceProvider BuildBridgeProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddBridgeDefaults();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddBridgeDefaults_Resolves_All_Three_Reporters()
    {
        using ServiceProvider provider = BuildBridgeProvider();

        Assert.NotNull(provider.GetService<IAuthFailureHandler>());
        Assert.NotNull(provider.GetService<IIndexerStatusReporter>());
        Assert.NotNull(provider.GetService<IRateLimitReporter>());
    }

    [Fact]
    public void AddBridgeDefaults_Returns_Default_Implementation_Types()
    {
        using ServiceProvider provider = BuildBridgeProvider();

        Assert.IsType<DefaultAuthFailureHandler>(provider.GetRequiredService<IAuthFailureHandler>());
        Assert.IsType<DefaultIndexerStatusReporter>(provider.GetRequiredService<IIndexerStatusReporter>());
        Assert.IsType<DefaultRateLimitReporter>(provider.GetRequiredService<IRateLimitReporter>());
    }

    [Fact]
    public void Bridge_Singletons_Return_Same_Instance()
    {
        using ServiceProvider provider = BuildBridgeProvider();

        IAuthFailureHandler auth1 = provider.GetRequiredService<IAuthFailureHandler>();
        IAuthFailureHandler auth2 = provider.GetRequiredService<IAuthFailureHandler>();
        Assert.Same(auth1, auth2);
    }

    [Fact]
    public async Task AuthHandler_Tracks_Full_Lifecycle()
    {
        using ServiceProvider provider = BuildBridgeProvider();
        IAuthFailureHandler handler = provider.GetRequiredService<IAuthFailureHandler>();

        Assert.Equal(AuthStatus.Unknown, handler.Status);

        await handler.HandleFailureAsync(new AuthFailure
        {
            ErrorCode = "TIDAL_AUTH",
            Message = "Token expired"
        });
        Assert.Equal(AuthStatus.Failed, handler.Status);

        await handler.HandleSuccessAsync();
        Assert.Equal(AuthStatus.Authenticated, handler.Status);
    }

    [Fact]
    public async Task RateLimitReporter_Tracks_RateLimit_Lifecycle()
    {
        using ServiceProvider provider = BuildBridgeProvider();
        IRateLimitReporter reporter = provider.GetRequiredService<IRateLimitReporter>();

        Assert.False(reporter.Status.IsRateLimited);

        await reporter.ReportRateLimitAsync(TimeSpan.FromSeconds(30));
        Assert.True(reporter.Status.IsRateLimited);
        Assert.NotNull(reporter.Status.ResetAt);

        await reporter.ReportRateLimitClearedAsync();
        Assert.False(reporter.Status.IsRateLimited);
    }

    [Fact]
    public async Task IndexerStatusReporter_Tracks_Search_Lifecycle()
    {
        using ServiceProvider provider = BuildBridgeProvider();
        IIndexerStatusReporter reporter = provider.GetRequiredService<IIndexerStatusReporter>();

        Assert.Equal(IndexerStatus.Idle, reporter.CurrentStatus);

        await reporter.ReportStatusAsync(IndexerStatus.Searching, "test query");
        Assert.Equal(IndexerStatus.Searching, reporter.CurrentStatus);

        await reporter.ReportStatusAsync(IndexerStatus.Idle);
        Assert.Equal(IndexerStatus.Idle, reporter.CurrentStatus);
    }

    [Fact]
    public async Task IndexerStatusReporter_Error_Then_Recovery()
    {
        using ServiceProvider provider = BuildBridgeProvider();
        IIndexerStatusReporter reporter = provider.GetRequiredService<IIndexerStatusReporter>();

        await reporter.ReportErrorAsync(new InvalidOperationException("API down"));
        Assert.Equal(IndexerStatus.Error, reporter.CurrentStatus);

        await reporter.ReportStatusAsync(IndexerStatus.Idle);
        Assert.Equal(IndexerStatus.Idle, reporter.CurrentStatus);
    }
}
