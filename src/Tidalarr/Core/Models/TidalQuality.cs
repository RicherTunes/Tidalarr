namespace Tidalarr.Core.Models;

/// <summary>
/// Represents the audio quality levels available on Tidal.
/// </summary>
public enum TidalQuality
{
    /// <summary>
    /// Low quality streaming (96 kbps AAC).
    /// </summary>
    Low = 0,

    /// <summary>
    /// High quality streaming (320 kbps AAC).
    /// </summary>
    High = 1,

    /// <summary>
    /// Lossless quality (FLAC 16-bit/44.1kHz CD quality).
    /// </summary>
    Lossless = 2,

    /// <summary>
    /// Hi-Res quality (FLAC up to 24-bit/192kHz).
    /// </summary>
    HiRes = 3
}
