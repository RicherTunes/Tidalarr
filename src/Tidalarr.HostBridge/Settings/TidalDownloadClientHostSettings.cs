using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalDownloadClientHostSettings
{
    [FieldDefinition(Integration.SettingsDisplay.Download.PreferredQualityOrder, Label = Integration.SettingsDisplay.Download.PreferredQualityLabel, Type = FieldType.Select, SelectOptions = typeof(TidalQualityHost), HelpText = "Audio quality requested from Tidal.")]
    public TidalQualityHost PreferredQuality { get; set; } = TidalQualityHost.Lossless;

    [FieldDefinition(Integration.SettingsDisplay.Download.DownloadPathOrder, Label = Integration.SettingsDisplay.Download.DownloadPathLabel, Type = FieldType.Path, HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Download.ChunkDelayOrder, Label = Integration.SettingsDisplay.Download.ChunkDelayLabel, Type = FieldType.Number, Unit = Integration.SettingsDisplay.Download.ChunkDelayUnit, Advanced = true, HelpText = "Delay between chunk requests in milliseconds. Use 0 for maximum speed, increase if rate-limited.")]
    public int DownloadDelay { get; set; } = 0;

    public Integration.TidalDownloadClientSettings ToCore()
    {
        return new Integration.TidalDownloadClientSettings
        {
            PreferredQuality = MapQuality(PreferredQuality),
            DownloadPath = DownloadPath,
            DownloadDelay = DownloadDelay
        };
    }

    private static Core.Models.TidalQuality MapQuality(TidalQualityHost q)
    {
        return q switch
        {
            TidalQualityHost.Low => Core.Models.TidalQuality.Low,
            TidalQualityHost.High => Core.Models.TidalQuality.High,
            TidalQualityHost.Lossless => Core.Models.TidalQuality.Lossless,
            TidalQualityHost.HiRes => Core.Models.TidalQuality.HiRes,
            _ => Core.Models.TidalQuality.Lossless
        };
    }
}
