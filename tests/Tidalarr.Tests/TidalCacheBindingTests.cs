using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Integration;
using Tidalarr.Infrastructure.Caching;
using Xunit;

namespace Tidalarr.Tests;

public class TidalCacheBindingTests
{
    [Fact]
    public void IStreamingResponseCache_ResolvesTo_TidalResponseCache()
    {
        var services = new ServiceCollection();
        TidalModule.RegisterServices(services);
        var sp = services.BuildServiceProvider();

        var cache = sp.GetService<IStreamingResponseCache>();
        Assert.NotNull(cache);
        Assert.IsType<TidalResponseCache>(cache);
    }
}



