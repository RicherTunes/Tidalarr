using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Core.Exceptions;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Integration
{
    /// <summary>
    /// Bridges Tidal's chunked streaming to the shared download orchestrator by
    /// assembling a contiguous audio stream from DASH chunks.
    /// </summary>
    public class TidalChunkStreamProvider(
        TidalStreamService streamService,
        TidalChunkDownloader chunkDownloader,
        TidalModelMapper mapper,
        TidalDownloadClientSettings settings) : IAudioStreamProvider
    {
        private readonly TidalStreamService _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
        private readonly TidalChunkDownloader _chunkDownloader = chunkDownloader ?? throw new ArgumentNullException(nameof(chunkDownloader));
        private readonly TidalModelMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        private readonly TidalDownloadClientSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        public async Task<AudioStreamResult> GetStreamAsync(string trackId, StreamingQuality? quality = null, CancellationToken cancellationToken = default)
        {
            TidalQuality tidalQuality = quality != null ? this._mapper.FromStreamingQuality(quality) : TidalQuality.Lossless;

            TidalManifest? manifest = null;
            try
            {
                manifest = await this._streamService.GetParsedManifestAsync(trackId, tidalQuality).ConfigureAwait(false);
            }
            catch (TidalStreamUnavailableException ex) when (ex.Reason.IsPermanent())
            {
                // A permanently-unavailable track (rights removed / delisted) must NOT be hidden by the
                // legacy fallback (which would re-resolve and fail the same way). Record it against the
                // ambient per-download scope so the download client can suppress the album, then propagate
                // so the orchestrator records this track as a failure (album → Failed, unchanged).
                TidalTerminalRestrictionScope.Record(trackId, ex.Reason);
                throw;
            }
            catch
            {
                // Transient / other: ignore and fall back to the legacy stream-info path.
            }

            if (manifest != null && manifest.ChunkUrls?.Any() == true)
            {
                int maxChunks = this._settings.GetEffectiveMaxConcurrentChunkDownloads();
                Stream assembled = await this._chunkDownloader.DownloadAndAssembleToFileStreamAsync(
                    manifest,
                    this._settings.DownloadDelay,
                    maxConcurrentChunkDownloads: maxChunks,
                    progress: null,
                    cancellationToken).ConfigureAwait(false);
                assembled.Position = 0;
                return new AudioStreamResult
                {
                    Stream = assembled,
                    TotalBytes = null,
                    SuggestedExtension = manifest.FileExtension?.TrimStart('.') ?? "m4a"
                };
            }

            TidalStreamInfo info;
            try
            {
                info = await this._streamService.GetStreamInfoAsync(trackId, tidalQuality).ConfigureAwait(false);
            }
            catch (TidalStreamUnavailableException ex) when (ex.Reason.IsPermanent())
            {
                TidalTerminalRestrictionScope.Record(trackId, ex.Reason);
                throw;
            }
            int maxChunksLegacy = this._settings.GetEffectiveMaxConcurrentChunkDownloads();
            Stream ms = await this._chunkDownloader.DownloadAndAssembleAsync(
                info,
                this._settings.DownloadDelay,
                maxConcurrentChunkDownloads: maxChunksLegacy,
                progress: null,
                cancellationToken).ConfigureAwait(false);
            return new AudioStreamResult
            {
                Stream = ms,
                TotalBytes = null,
                SuggestedExtension = info.FileExtension?.TrimStart('.') ?? "m4a"
            };
        }

    }
}
