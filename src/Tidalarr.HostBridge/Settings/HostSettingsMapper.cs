namespace Tidalarr.HostBridge.Settings;

public interface IHostSettingsMapper
{
    Integration.TidalarrSettings ToCore(TidalarrHostSettings host);
    Integration.TidalIndexerSettings ToCore(TidalIndexerHostSettings host);
    Integration.TidalDownloadClientSettings ToCore(TidalDownloadClientHostSettings host);
}

public sealed class HostSettingsMapper : IHostSettingsMapper
{
    public Integration.TidalarrSettings ToCore(TidalarrHostSettings host)
    {
        return (host ?? new TidalarrHostSettings()).ToCore();
    }

    public Integration.TidalIndexerSettings ToCore(TidalIndexerHostSettings host)
    {
        return (host ?? new TidalIndexerHostSettings()).ToCore();
    }

    public Integration.TidalDownloadClientSettings ToCore(TidalDownloadClientHostSettings host)
    {
        return (host ?? new TidalDownloadClientHostSettings()).ToCore();
    }
}

public static class HostSettingsMapperExtensions
{
    public static object ToCoreObject(this IHostSettingsMapper mapper, object hostSettings)
    {
        return hostSettings switch
        {
            TidalarrHostSettings s => mapper.ToCore(s),
            TidalIndexerHostSettings s => mapper.ToCore(s),
            TidalDownloadClientHostSettings s => mapper.ToCore(s),
            _ => hostSettings
        };
    }
}

