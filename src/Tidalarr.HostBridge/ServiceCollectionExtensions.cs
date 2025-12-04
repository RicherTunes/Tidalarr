using Microsoft.Extensions.DependencyInjection;

namespace Tidalarr.HostBridge;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTidalarrHostBridgeServices(this IServiceCollection services)
    {
        _ = services.AddSingleton<Settings.IHostSettingsMapper, Settings.HostSettingsMapper>();
        return services;
    }
}

