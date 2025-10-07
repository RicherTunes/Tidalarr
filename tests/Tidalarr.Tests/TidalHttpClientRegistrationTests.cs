using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Xunit;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

public class TidalHttpClientRegistrationTests
{
    [Fact]
    public void IHttpClientFactory_IsRegistered_ByModule()
    {
        var services = new ServiceCollection();
        TidalModule.RegisterServices(services);

        // DI contains the HttpClient factory registration
        Assert.Contains(services, s => s.ServiceType == typeof(IHttpClientFactory));
    }
}




