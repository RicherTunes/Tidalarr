// Example only; not part of build.
// Demonstrates how Tidalarr could construct the SimpleDownloadOrchestrator using existing services.

/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Services.Download;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Mappers;
using Tidalarr.Domain.Streaming;

public static class TidalOrchestratorFactory
{
    public static SimpleDownloadOrchestrator Create(
        HttpClient httpClient,
        ITidalCore api,
        TidalStreamService streamService,
        TidalModelMapper mapper)
    {
        return new SimpleDownloadOrchestrator(
            serviceName: "Tidal",
            httpClient: httpClient,
            getAlbumAsync: async id => mapper.ToStreamingAlbum(await api.GetAlbumWithTracksAsync(id)),
            getTrackAsync: async id => mapper.ToStreamingTrack(await api.GetTrackAsync(id)),
            getAlbumTrackIdsAsync: async id =>
            {
                var album = await api.GetAlbumWithTracksAsync(id);
                return (IReadOnlyList<string>) (album.Tracks ?? new List<Tidalarr.Core.Models.TidalTrackInfo>()).Select(t => t.Id).ToList();
            },
            getStreamAsync: async (trackId, quality) =>
            {
                // For Tidal, streams are DASH chunks; the simple orchestrator expects a final URL.
                // This example returns the first chunk URL and extension as a placeholder.
                // Production should continue to use TidalChunkDownloader for assembly.
                var tidalQ = mapper.FromStreamingQuality(quality ?? new StreamingQuality { Bitrate = 320 });
                var info = await streamService.GetStreamInfoAsync(trackId, tidalQ);
                var url = info.ChunkUrls.FirstOrDefault() ?? string.Empty;
                var ext = info.FileExtension?.TrimStart('.') ?? "flac";
                return (url, ext);
            }
        );
    }
}
*/


