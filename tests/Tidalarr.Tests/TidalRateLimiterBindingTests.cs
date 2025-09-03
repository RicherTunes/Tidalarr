using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Services.Performance;
using Tidalarr.Integration;
using Tidalarr.Infrastructure.Performance;
using Xunit;

namespace Tidalarr.Tests;

public class TidalRateLimiterBindingTests
{
    [Fact]
    public void AdaptiveRateLimiter_ResolvesTo_TidalRateLimiter()
    {
        var services = new ServiceCollection();
        TidalModule.RegisterServices(services);
        var sp = services.BuildServiceProvider();

        var limiter = sp.GetService<AdaptiveRateLimiter>();
        Assert.NotNull(limiter);
        Assert.IsType<TidalRateLimiter>(limiter);
    }
}

