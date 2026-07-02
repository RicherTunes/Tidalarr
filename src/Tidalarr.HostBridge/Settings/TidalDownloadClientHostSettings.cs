using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalDownloadClientHostSettings
{
    [FieldDefinition(Integration.SettingsDisplay.Download.PreferredQualityOrder, Label = Integration.SettingsDisplay.Download.PreferredQualityLabel, Type = FieldType.Select, SelectOptions = typeof(TidalQualityHost), HelpText = Integration.SettingsDisplay.Download.PreferredQualityHelpText)]
    public TidalQualityHost PreferredQuality { get; set; } = TidalQualityHost.Lossless;

    [FieldDefinition(Integration.SettingsDisplay.Download.DownloadPathOrder, Label = Integration.SettingsDisplay.Download.DownloadPathLabel, Type = FieldType.Path, HelpText = Integration.SettingsDisplay.Download.DownloadPathHelpText)]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Download.ExtractFlacOrder, Label = Integration.SettingsDisplay.Download.ExtractFlacLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = Integration.SettingsDisplay.Download.ExtractFlacHelpText)]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(Integration.SettingsDisplay.Download.SaveSyncedLyricsOrder, Label = Integration.SettingsDisplay.Download.SaveSyncedLyricsLabel, Type = FieldType.Checkbox, Advanced = true)]
    public bool SaveSyncedLyrics { get; set; } = true;

    [FieldDefinition(Integration.SettingsDisplay.Download.UseLrclibOrder, Label = Integration.SettingsDisplay.Download.UseLrclibLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = Integration.SettingsDisplay.Download.UseLrclibHelpText)]
    public bool UseLRCLIB { get; set; } = false;

    [FieldDefinition(Integration.SettingsDisplay.Download.ChunkDelayOrder, Label = Integration.SettingsDisplay.Download.ChunkDelayLabel, Type = FieldType.Number, Unit = Integration.SettingsDisplay.Download.ChunkDelayUnit, Advanced = true, HelpText = Integration.SettingsDisplay.Download.ChunkDelayHelpText)]
    public int DownloadDelay { get; set; } = 0;

    [FieldDefinition(Integration.SettingsDisplay.Download.MaxConcurrentTrackDownloadsOrder, Label = Integration.SettingsDisplay.Download.MaxConcurrentTrackDownloadsLabel, Type = FieldType.Number, Advanced = true, HelpText = Integration.SettingsDisplay.Download.MaxConcurrentTrackDownloadsHelpText)]
    public int MaxConcurrentTrackDownloads { get; set; } = 2;

    [FieldDefinition(Integration.SettingsDisplay.Download.MaxConcurrentChunkDownloadsOrder, Label = Integration.SettingsDisplay.Download.MaxConcurrentChunkDownloadsLabel, Type = FieldType.Number, Advanced = true, HelpText = Integration.SettingsDisplay.Download.MaxConcurrentChunkDownloadsHelpText)]
    public int MaxConcurrentChunkDownloads { get; set; } = 2;

    public Integration.TidalDownloadClientSettings ToCore()
    {
        return new Integration.TidalDownloadClientSettings
        {
            PreferredQuality = MapQuality(PreferredQuality),
            DownloadPath = DownloadPath,
            ExtractFlac = ExtractFlac,
            SaveSyncedLyrics = SaveSyncedLyrics,
            UseLRCLIB = UseLRCLIB,
            DownloadDelay = DownloadDelay,
            MaxConcurrentTrackDownloads = MaxConcurrentTrackDownloads,
            MaxConcurrentChunkDownloads = MaxConcurrentChunkDownloads
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
