using Microsoft.Extensions.DependencyInjection;
using Lidarr.Plugin.Common.Services.Network;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalNetworkResilienceRegistrationTests
{
    [Fact]
    public void NetworkResilienceService_IsRegistered_ByModule()
    {
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);
        ServiceProvider sp = services.BuildServiceProvider();

        NetworkResilienceService? nrs = sp.GetService<NetworkResilienceService>();
        Assert.NotNull(nrs);
    }
}




