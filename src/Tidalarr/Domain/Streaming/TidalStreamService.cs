using System;
using System.Collections.Generic;
using System.Linq;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class TidalStreamService
{
    private readonly ITidalCore _apiClient;
    private readonly TidalManifestParser _manifestParser;

    public TidalStreamService(ITidalCore apiClient, TidalManifestParser manifestParser)
    {
        _apiClient = apiClient;
        _manifestParser = manifestParser;
    }

    public Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality)
        => _apiClient.GetStreamInfoAsync(trackId, quality);

    public Task<TidalStreamInfo> GetStreamInfoWithManifestParsingAsync(string trackId, TidalQuality quality, string manifest, string manifestMimeType)
    {
        var parsed = _manifestParser.ParseManifest(manifest, manifestMimeType);
        var info = new TidalStreamInfo(
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
            var playback = await _apiClient.GetPlaybackInfoAsync(trackId, quality);
            var parsed = _manifestParser.ParseManifest(playback.manifest, playback.manifestMimeType);
            var encryptionType = playback.encryptionType;
            var isEncrypted = !string.IsNullOrWhiteSpace(encryptionType) && !string.Equals(encryptionType, "NONE", StringComparison.OrdinalIgnoreCase);
            var combinedSecurityToken = !string.IsNullOrWhiteSpace(playback.securityToken)
                ? playback.securityToken
                : parsed.SecurityToken;

            var enriched = parsed with
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
            var playback = await _apiClient.GetPlaybackInfoAsync(trackId, quality);
            var parsed = _manifestParser.ParseManifest(playback.manifest, playback.manifestMimeType);
            var encryptionType = playback.encryptionType;
            var isEncrypted = !string.IsNullOrWhiteSpace(encryptionType) && !string.Equals(encryptionType, "NONE", StringComparison.OrdinalIgnoreCase);
            var combinedSecurityToken = !string.IsNullOrWhiteSpace(playback.securityToken)
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
            var info = await GetStreamInfoAsync(trackId, quality);
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
            var streamInfo = await GetStreamInfoAsync(trackId, quality);
            return streamInfo.ChunkUrls.Any() && !string.IsNullOrEmpty(streamInfo.FileExtension);
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<TidalQuality>> GetAvailableQualitiesForTrackAsync(string trackId)
    {
        var available = new List<TidalQuality>();
        var order = new[] { TidalQuality.HiRes, TidalQuality.Lossless, TidalQuality.High, TidalQuality.Low };
        foreach (var q in order)
        {
            if (await ValidateStreamAvailabilityAsync(trackId, q))
            {
                available.Add(q);
            }
        }
        return available;
    }
}








