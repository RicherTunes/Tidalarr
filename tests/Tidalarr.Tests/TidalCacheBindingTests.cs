using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Integration;
using Tidalarr.Infrastructure.Caching;

namespace Tidalarr.Tests;

public class TidalCacheBindingTests
{
    [Fact]
    public void IStreamingResponseCache_ResolvesTo_TidalResponseCache()
    {
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);
        ServiceProvider sp = services.BuildServiceProvider();

        IStreamingResponseCache? cache = sp.GetService<IStreamingResponseCache>();
        Assert.NotNull(cache);
        _ = Assert.IsType<TidalResponseCache>(cache);
    }
}



