namespace Tidalarr.Core.Models;

/// <summary>
/// Represents a parsed DASH manifest for Tidal streaming.
/// </summary>
public record TidalManifest(
    string[] ChunkUrls,
    string Codec,
    string MimeType,
    string FileExtension,
    int SampleRate,
    bool IsEncrypted,
    string? KeyId,
    string? SecurityToken);
