using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalDownloadClientHostSettings
{
    [FieldDefinition(20, Label = "Preferred Quality", Type = FieldType.Select, SelectOptions = typeof(TidalQualityHost), HelpText = "Audio quality requested from Tidal.")]
    public TidalQualityHost PreferredQuality { get; set; } = TidalQualityHost.Lossless;

    [FieldDefinition(21, Label = "Download Path", Type = FieldType.Path, HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(27, Label = "Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true, HelpText = "Delay between chunk requests used for throttling.")]
    public int DownloadDelay { get; set; } = 1000;

    [FieldDefinition(28, Label = "Min Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
    public int DownloadDelayMin { get; set; } = 500;

    [FieldDefinition(29, Label = "Max Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
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
