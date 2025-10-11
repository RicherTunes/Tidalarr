using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalDownloadClientHostSettings
{
    [FieldDefinition(21, Label = "Download Path", Type = FieldType.Path, HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(27, Label = "Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true, HelpText = "Delay between chunk requests used for throttling.")]
    public int DownloadDelay { get; set; } = 1000;

    [FieldDefinition(28, Label = "Min Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
    public int DownloadDelayMin { get; set; } = 500;

    [FieldDefinition(29, Label = "Max Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
    public int DownloadDelayMax { get; set; } = 2000;
}

