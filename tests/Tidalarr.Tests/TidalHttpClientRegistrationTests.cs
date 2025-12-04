using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalHttpClientRegistrationTests
{
    [Fact]
    public void IHttpClientFactory_IsRegistered_ByModule()
    {
        ServiceCollection services = new();
        TidalModule.RegisterServices(services);

        // DI contains the HttpClient factory registration
        Assert.Contains(services, s => s.ServiceType == typeof(IHttpClientFactory));
    }
}




