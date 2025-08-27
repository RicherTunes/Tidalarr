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
    
    public async Task<TidalStreamInfo> GetStreamInfoAsync(string trackId, TidalQuality quality)
    {
        // Get playback info from Tidal API
        var playbackInfo = await _apiClient.GetStreamInfoAsync(trackId, quality);
        
        // If API client already parsed the manifest, return as-is
        if (playbackInfo.ChunkUrls.Length > 1)
            return playbackInfo;
        
        // Otherwise, we need to parse the manifest ourselves
        // This is a fallback for when API client doesn't parse manifests
        return playbackInfo;
    }
    
    public async Task<TidalStreamInfo> GetStreamInfoWithManifestParsingAsync(string trackId, TidalQuality quality, string manifest, string manifestMimeType)
    {
        try
        {
            var parsedManifest = _manifestParser.ParseManifest(manifest, manifestMimeType);
            
            return new TidalStreamInfo(
                TrackId: trackId,
                ChunkUrls: parsedManifest.ChunkUrls,
                FileExtension: parsedManifest.FileExtension,
                MimeType: parsedManifest.MimeType,
                IsEncrypted: parsedManifest.IsEncrypted,
                SecurityToken: parsedManifest.EncryptionKey
            );
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to process stream manifest for track {trackId}: {ex.Message}", ex);
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
        var availableQualities = new List<TidalQuality>();
        
        // Test each quality to see what's actually available
        var qualitiesToTest = new[] { TidalQuality.HiRes, TidalQuality.Lossless, TidalQuality.High, TidalQuality.Low };
        
        foreach (var quality in qualitiesToTest)
        {
            if (await ValidateStreamAvailabilityAsync(trackId, quality))
            {
                availableQualities.Add(quality);
            }
        }
        
        return availableQualities;
    }
}
