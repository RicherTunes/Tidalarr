namespace Tidalarr.Core.Models;

/// <summary>
/// Stream information for a Tidal track. <see cref="DeliveredQuality"/> is
/// the quality Tidal actually returned (may be lower than what was requested
/// when the user's subscription tier doesn't grant the higher option).
/// </summary>
public record TidalStreamInfo(
    string TrackId,
    string[] ChunkUrls,
    string FileExtension,
    string MimeType,
    bool IsEncrypted,
    string? SecurityToken,
    TidalQuality? DeliveredQuality = null);
