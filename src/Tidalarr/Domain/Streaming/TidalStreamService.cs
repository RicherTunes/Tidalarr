using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class TidalStreamService(ITidalCore apiClient, TidalManifestParser manifestParser)
{
    private readonly ITidalCore _apiClient = apiClient;
    private readonly TidalManifestParser _manifestParser = manifestParser;

    public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality)
    {
        return this._apiClient.GetStreamInfoAsync(trackId, quality);
    }

    public Task<TidalStreamInfo> GetStreamInfoWithManifestParsingAsync(string trackId, TidalQuality quality, string manifest, string manifestMimeType)
    {
        TidalManifest parsed = this._manifestParser.ParseManifest(manifest, manifestMimeType);
        TidalStreamInfo info = new(
            TrackId: trackId,
            ChunkUrls: parsed.ChunkUrls,
            FileExtension: parsed.FileExtension,
            MimeType: parsed.MimeType,
            IsEncrypted: parsed.IsEncrypted,
            SecurityToken: parsed.SecurityToken);
        return Task.FromResult(info);
    }

    // New: unified method that prefers raw playback-info + manifest parsing, falling back to legacy API stream info
    public async Task<TidalStreamInfo> GetStreamInfoParsedAsync(string trackId, TidalQuality quality)
    {
        try
        {
            TidalPlaybackInfoDto playback = await this._apiClient.GetPlaybackInfoAsync(trackId, quality);
            TidalManifest parsed = this._manifestParser.ParseManifest(playback.manifest ?? string.Empty, playback.manifestMimeType ?? string.Empty);
            string? encryptionType = playback.encryptionType;
            bool isEncrypted = !string.IsNullOrWhiteSpace(encryptionType) && !string.Equals(encryptionType, "NONE", StringComparison.OrdinalIgnoreCase);
            string? combinedSecurityToken = !string.IsNullOrWhiteSpace(playback.securityToken)
                ? playback.securityToken
                : parsed.SecurityToken;

            TidalManifest enriched = parsed with
            {
                IsEncrypted = parsed.IsEncrypted || isEncrypted,
                SecurityToken = combinedSecurityToken
            };

            return new TidalStreamInfo(
                TrackId: trackId,
                ChunkUrls: enriched.ChunkUrls,
                FileExtension: enriched.FileExtension,
                MimeType: enriched.MimeType,
                IsEncrypted: enriched.IsEncrypted,
                SecurityToken: enriched.SecurityToken);
        }
        catch (NotSupportedException)
        {
            // Fallback for older stubs/implementations
            return await GetStreamInfoAsync(trackId, quality);
        }
        catch (Exception)
        {
            // On parse or fetch error, fall back to legacy method as well
            return await GetStreamInfoAsync(trackId, quality);
        }
    }


    // Provide parsed manifest with codec/container details for enhanced downloads
    public async Task<TidalManifest> GetParsedManifestAsync(string trackId, TidalQuality quality)
    {
        try
        {
            TidalPlaybackInfoDto playback = await this._apiClient.GetPlaybackInfoAsync(trackId, quality);
            TidalManifest parsed = this._manifestParser.ParseManifest(playback.manifest ?? string.Empty, playback.manifestMimeType ?? string.Empty);
            string? encryptionType = playback.encryptionType;
            bool isEncrypted = !string.IsNullOrWhiteSpace(encryptionType) && !string.Equals(encryptionType, "NONE", StringComparison.OrdinalIgnoreCase);
            string? combinedSecurityToken = !string.IsNullOrWhiteSpace(playback.securityToken)
                ? playback.securityToken
                : parsed.SecurityToken;

            return parsed with
            {
                IsEncrypted = parsed.IsEncrypted || isEncrypted,
                SecurityToken = combinedSecurityToken
            };
        }
        catch (NotSupportedException)
        {
            // Fallback: build a minimal manifest from legacy stream info
            TidalStreamInfo info = await GetStreamInfoAsync(trackId, quality);
            return new TidalManifest(
                ChunkUrls: info.ChunkUrls,
                Codec: "MP4A",
                MimeType: info.MimeType,
                FileExtension: info.FileExtension,
                SampleRate: 44100,
                IsEncrypted: info.IsEncrypted,
                KeyId: null,
                SecurityToken: info.SecurityToken);
        }
    }


    public async Task<bool> ValidateStreamAvailabilityAsync(string trackId, TidalQuality quality)
    {
        try
        {
            TidalStreamInfo streamInfo = await GetStreamInfoAsync(trackId, quality);
            return streamInfo.ChunkUrls.Any() && !string.IsNullOrEmpty(streamInfo.FileExtension);
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<TidalQuality>> GetAvailableQualitiesForTrackAsync(string trackId)
    {
        List<TidalQuality> available = [];
        TidalQuality[] order = [TidalQuality.HiRes, TidalQuality.Lossless, TidalQuality.High, TidalQuality.Low];
        foreach (TidalQuality q in order)
        {
            if (await ValidateStreamAvailabilityAsync(trackId, q))
            {
                available.Add(q);
            }
        }
        return available;
    }
}








