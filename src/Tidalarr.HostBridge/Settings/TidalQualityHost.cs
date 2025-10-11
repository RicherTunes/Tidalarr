using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public enum TidalQualityHost
{
    [FieldOption(Label = "Low (AAC 96 kbps)")]
    Low,
    [FieldOption(Label = "High (AAC 320 kbps)")]
    High,
    [FieldOption(Label = "Lossless (FLAC 16-bit)")]
    Lossless,
    [FieldOption(Label = "Hi-Res (FLAC 24-bit)")]
    HiRes
}

