using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Download;
using Lidarr.Plugin.Common.Utilities;

namespace Tidalarr.Application.Services;

/// <summary>
/// Tidal's download orchestrator: Common's <see cref="SimpleDownloadOrchestrator"/> (shared album loop,
/// SSRF guard, retry-with-resume, atomic move, tagging/artwork) plus the
/// <see cref="ValidateDownloadedPayload"/> seam wired to Common's canonical
/// <see cref="DownloadPayloadValidator"/>. Without this, a CDN/API error body served as HTTP 200 (an HTML
/// soft-404, a JSON problem document) would land on disk as a fake <c>.m4a</c>/<c>.flac</c> and reach
/// Lidarr's import — the failure class qobuz hit live. A rejected payload fails just that track (deleted
/// from disk), feeding the AlbumCompletionPolicy incomplete⇒Failed contract.
/// </summary>
public sealed class TidalDownloadOrchestrator : SimpleDownloadOrchestrator
{
    public TidalDownloadOrchestrator(
        string serviceName,
        HttpClient httpClient,
        Func<string, Task<StreamingAlbum>> getAlbumAsync,
        Func<string, Task<StreamingTrack>> getTrackAsync,
        Func<string, Task<IReadOnlyList<string>>> getAlbumTrackIdsAsync,
        Func<string, StreamingQuality?, Task<(string Url, string Extension)>> getStreamAsync,
        int maxConcurrentTracks,
        IAudioStreamProvider? streamProvider,
        IAudioMetadataApplier? metadataApplier,
        IAudioPostProcessor? postProcessor,
        IDownloadTelemetrySink? telemetrySink)
        : base(
            serviceName: serviceName,
            httpClient: httpClient,
            getAlbumAsync: getAlbumAsync,
            getTrackAsync: getTrackAsync,
            getAlbumTrackIdsAsync: getAlbumTrackIdsAsync,
            getStreamAsync: getStreamAsync,
            maxConcurrentTracks: maxConcurrentTracks,
            streamProvider: streamProvider,
            metadataApplier: metadataApplier,
            logger: null,
            postProcessor: postProcessor,
            telemetrySink: telemetrySink)
    {
    }

    /// <summary>
    /// Rejects non-audio payloads via <see cref="DownloadPayloadValidator.ValidateFileOrThrow"/>
    /// (text/HTML/JSON detection + audio magic bytes: fLaC, ftyp/M4A, OggS, RIFF, ID3). Runs with the
    /// FINAL file path after post-processing on every download path; stateless, so safe under the
    /// concurrent album loop.
    /// </summary>
    protected override void ValidateDownloadedPayload(string filePath, StreamingTrack? track)
    {
        DownloadPayloadValidator.ValidateFileOrThrow(filePath);
    }
}
