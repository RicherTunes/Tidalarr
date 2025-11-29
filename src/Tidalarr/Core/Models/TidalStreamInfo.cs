namespace Tidalarr.Core.Models;

/// <summary>
/// Represents stream information for a Tidal track.
/// </summary>
public record TidalStreamInfo(
    string TrackId,
    string[] ChunkUrls,
    string FileExtension,
    string MimeType,
    bool IsEncrypted,
    string? SecurityToken);
