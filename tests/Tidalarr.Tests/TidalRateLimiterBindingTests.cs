using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Services.Performance;
using Tidalarr.Integration;
using Tidalarr.Infrastructure.Performance;

namespace Tidalarr.Tests;

public class TidalRateLimiterBindingTests
{
    [Fact]
    public void IUniversalAdaptiveRateLimiter_ResolvesTo_TidalRateLimiter()
    {
        ServiceCollection services = new ServiceCollection();
        TidalModule.RegisterServices(services);
        ServiceProvider sp = services.BuildServiceProvider();

        IUniversalAdaptiveRateLimiter? limiter = sp.GetService<IUniversalAdaptiveRateLimiter>();
        Assert.NotNull(limiter);
        _ = Assert.IsType<TidalRateLimiter>(limiter);
    }
}




