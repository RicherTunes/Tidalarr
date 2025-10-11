using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalDownloadClientHostSettings
{
    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Download.PreferredQualityOrder, Label = Tidalarr.Integration.SettingsDisplay.Download.PreferredQualityLabel, Type = FieldType.Select, SelectOptions = typeof(TidalQualityHost), HelpText = "Audio quality requested from Tidal.")]
    public TidalQualityHost PreferredQuality { get; set; } = TidalQualityHost.Lossless;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Download.DownloadPathOrder, Label = Tidalarr.Integration.SettingsDisplay.Download.DownloadPathLabel, Type = FieldType.Path, HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayOrder, Label = Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayLabel, Type = FieldType.Number, Unit = Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayUnit, Advanced = true, HelpText = "Delay between chunk requests used for throttling.")]
    public int DownloadDelay { get; set; } = 1000;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayMinOrder, Label = Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayMinLabel, Type = FieldType.Number, Unit = Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayMinUnit, Advanced = true)]
    public int DownloadDelayMin { get; set; } = 500;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayMaxOrder, Label = Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayMaxLabel, Type = FieldType.Number, Unit = Tidalarr.Integration.SettingsDisplay.Download.ChunkDelayMaxUnit, Advanced = true)]
    public int DownloadDelayMax { get; set; } = 2000;

    public Tidalarr.Integration.TidalDownloadClientSettings ToCore()
    {
        return new Tidalarr.Integration.TidalDownloadClientSettings
        {
            PreferredQuality = MapQuality(PreferredQuality),
            DownloadPath = DownloadPath,
            DownloadDelay = DownloadDelay,
            DownloadDelayMin = DownloadDelayMin,
            DownloadDelayMax = DownloadDelayMax
        };
    }

    private static Tidalarr.Core.Models.TidalQuality MapQuality(TidalQualityHost q) => q switch
    {
        TidalQualityHost.Low => Tidalarr.Core.Models.TidalQuality.Low,
        TidalQualityHost.High => Tidalarr.Core.Models.TidalQuality.High,
        TidalQualityHost.Lossless => Tidalarr.Core.Models.TidalQuality.Lossless,
        TidalQualityHost.HiRes => Tidalarr.Core.Models.TidalQuality.HiRes,
        _ => Tidalarr.Core.Models.TidalQuality.Lossless
    };
}
