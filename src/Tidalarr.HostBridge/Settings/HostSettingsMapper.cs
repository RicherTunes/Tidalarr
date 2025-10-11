namespace Tidalarr.HostBridge.Settings;

public interface IHostSettingsMapper
{
    Tidalarr.Integration.TidalarrSettings ToCore(TidalarrHostSettings host);
    Tidalarr.Integration.TidalIndexerSettings ToCore(TidalIndexerHostSettings host);
    Tidalarr.Integration.TidalDownloadClientSettings ToCore(TidalDownloadClientHostSettings host);
}

public sealed class HostSettingsMapper : IHostSettingsMapper
{
    public Tidalarr.Integration.TidalarrSettings ToCore(TidalarrHostSettings host)
        => (host ?? new TidalarrHostSettings()).ToCore();

    public Tidalarr.Integration.TidalIndexerSettings ToCore(TidalIndexerHostSettings host)
        => (host ?? new TidalIndexerHostSettings()).ToCore();

    public Tidalarr.Integration.TidalDownloadClientSettings ToCore(TidalDownloadClientHostSettings host)
        => (host ?? new TidalDownloadClientHostSettings()).ToCore();
}

public static class HostSettingsMapperExtensions
{
    public static object ToCoreObject(this IHostSettingsMapper mapper, object hostSettings)
        => hostSettings switch
        {
            TidalarrHostSettings s => mapper.ToCore(s),
            TidalIndexerHostSettings s => mapper.ToCore(s),
            TidalDownloadClientHostSettings s => mapper.ToCore(s),
            _ => hostSettings
        };
}

