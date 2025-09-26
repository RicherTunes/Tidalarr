using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Services.Network;
using Xunit;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalNetworkResilienceRegistrationTests
{
    [Fact]
    public void NetworkResilienceService_IsRegistered_ByModule()
    {
        var services = new ServiceCollection();
        TidalModule.RegisterServices(services);
        var sp = services.BuildServiceProvider();

        var nrs = sp.GetService<NetworkResilienceService>();
        Assert.NotNull(nrs);
    }
}



