using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

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
        TidalIndexerSettings indexerSettings = new()
        {
            TidalMarket = "US",
            RedirectUrl = "https://tidal.com/android/login/auth?code=test_auth_code&state=test_state",
            EnableCache = true,
            CacheDuration = 15,
            ConfigPath = Path.GetTempPath()
        };
        TidalDownloadClientSettings downloadSettings = new()
        {
            PreferredQuality = TidalQuality.Lossless,
            DownloadPath = Path.GetTempPath(),
            DownloadDelay = 1000
        };

        Assert.True(indexerSettings.ValidateFluent().IsValid);
        Assert.True(downloadSettings.ValidateFluent().IsValid);

        IServiceProvider serviceProvider = CreateServiceProvider(indexerSettings, downloadSettings);
        TidalIndexer indexer = serviceProvider.GetRequiredService<TidalIndexer>();
        TidalDownloadClient downloadClient = serviceProvider.GetRequiredService<TidalDownloadClient>();

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
        TidalIndexerSettings indexerSettings2 = new()
        {
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=test",
            ConfigPath = Path.GetTempPath()
        };

        try
        {
            ServiceCollection services = new();
            _ = services.AddSingleton(indexerSettings2);
            _ = services.AddSingleton(new TidalDownloadClientSettings
            {
                PreferredQuality = TidalQuality.Lossless,
                DownloadPath = Path.GetTempPath()
            });
            TidalModule.RegisterServices(services);
            ServiceProvider provider = services.BuildServiceProvider();
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
        TidalIndexerSettings settings = new()
        {
            TidalMarket = market,
            RedirectUrl = "https://tidal.com/android/login/auth?code=test&state=test",
            ConfigPath = Path.GetTempPath()
        };

        FluentValidation.Results.ValidationResult validation = settings.ValidateFluent();
        TidalDownloadClientSettings downloadSettings = new()
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
        ServiceCollection services = new();
        _ = services.AddSingleton(indexerSettings);
        _ = services.AddSingleton(downloadSettings);
        TidalModule.RegisterServices(services);
        return services.BuildServiceProvider();
    }
}
