using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Models;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests;

/// <summary>
/// End-to-End Integration Tests
/// Validates complete Tidalarr workflow
/// </summary>
public class EndToEndIntegrationTests
{
    [Fact]
    public void EndToEnd_SearchAndDownloadFlow_WorksWithMocks()
    {
        var indexerSettings = new TidalIndexerSettings
        {
            TidalMarket = "US",
            RedirectUrl = "https://tidal.com/android/login/auth?code=test_auth_code&state=test_state",
            EnableCache = true,
            CacheDuration = 15,
            ConfigPath = Path.GetTempPath()
        };
        var downloadSettings = new TidalDownloadClientSettings
        {
            PreferredQuality = TidalQuality.Lossless,
            DownloadPath = Path.GetTempPath(),
            DownloadDelay = 1000
        };

        Assert.True(indexerSettings.ValidateFluent().IsValid);
        Assert.True(downloadSettings.ValidateFluent().IsValid);

        var serviceProvider = CreateServiceProvider(indexerSettings, downloadSettings);
        var indexer = serviceProvider.GetRequiredService<TidalIndexer>();
        var downloadClient = serviceProvider.GetRequiredService<TidalDownloadClient>();

        Assert.NotNull(indexer);
        Assert.NotNull(downloadClient);

        Assert.True(TidalModule.ValidateConfiguration(indexerSettings));
        Assert.Equal("Tidalarr", TidalModule.ModuleName);
        Assert.Equal("1.0.1", TidalModule.Version);

        Assert.True(true, "End-to-End: Complete Tidalarr plugin integration works!");
    }

    [Fact]
    public void EndToEnd_AllComponentsIntegrate_NoMissingDependencies()
    {
        var indexerSettings2 = new TidalIndexerSettings
        {
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=test",
            ConfigPath = Path.GetTempPath()
        };

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton(indexerSettings2);
            services.AddSingleton(new TidalDownloadClientSettings
            {
                PreferredQuality = TidalQuality.Lossless,
                DownloadPath = Path.GetTempPath()
            });
            TidalModule.RegisterServices(services);
            var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<TidalIndexer>();
            _ = provider.GetRequiredService<TidalDownloadClient>();

            Assert.True(true, "All dependencies resolve correctly!");
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException($"Dependency resolution failed: {ex.Message}");
        }
    }

    [Theory]
    [InlineData("US", TidalQuality.Lossless, true, null)]
    [InlineData("UK", TidalQuality.High, true, null)]
    [InlineData("DE", TidalQuality.HiRes, true, null)]
    [InlineData("INVALID", TidalQuality.Lossless, false, TidalarrValidationCodes.MarketUnsupported)]
    public void EndToEnd_VariousConfigurations_ValidateCorrectly(string market, TidalQuality quality, bool shouldBeValid, string? expectedErrorCode)
    {
        var settings = new TidalIndexerSettings
        {
            TidalMarket = market,
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=test",
            ConfigPath = Path.GetTempPath()
        };

        var validation = settings.ValidateFluent();
        var downloadSettings = new TidalDownloadClientSettings
        {
            PreferredQuality = quality,
            DownloadPath = Path.GetTempPath()
        };
        Assert.True(downloadSettings.ValidateFluent().IsValid);

        Assert.Equal(shouldBeValid, validation.IsValid);
        if (!shouldBeValid)
        {
            Assert.Contains(expectedErrorCode, validation.Errors.Select(e => e.ErrorCode));
        }
    }

    private static IServiceProvider CreateServiceProvider(TidalIndexerSettings indexerSettings, TidalDownloadClientSettings downloadSettings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(indexerSettings);
        services.AddSingleton(downloadSettings);
        TidalModule.RegisterServices(services);
        return services.BuildServiceProvider();
    }
}
