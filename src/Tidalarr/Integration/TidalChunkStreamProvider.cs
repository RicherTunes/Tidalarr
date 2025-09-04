using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Integration
{
    /// <summary>
    /// Bridges Tidal's chunked streaming to the shared download orchestrator by
    /// assembling a contiguous audio stream from DASH chunks.
    /// </summary>
    public class TidalChunkStreamProvider : IAudioStreamProvider
    {
        private readonly TidalStreamService _streamService;
        private readonly TidalChunkDownloader _chunkDownloader;
        private readonly TidalModelMapper _mapper;

        public TidalChunkStreamProvider(TidalStreamService streamService, TidalChunkDownloader chunkDownloader, TidalModelMapper mapper)
        {
            _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
            _chunkDownloader = chunkDownloader ?? throw new ArgumentNullException(nameof(chunkDownloader));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<AudioStreamResult> GetStreamAsync(string trackId, StreamingQuality? quality = null, CancellationToken cancellationToken = default)
        {
            // Map shared StreamingQuality to TidalQuality
            var tidalQuality = quality != null ? _mapper.FromStreamingQuality(quality) : TidalQuality.Lossless;

            // Prefer manifest parsing path when available
            TidalManifest? manifest = null;
            try
            {
                manifest = await _streamService.GetParsedManifestAsync(trackId, tidalQuality).ConfigureAwait(false);
            }
            catch
            {
                // Fallback to legacy stream info path
            }

            if (manifest != null && manifest.ChunkUrls?.Any() == true)
            {
                var assembled = await _chunkDownloader.DownloadAndAssembleAsync(manifest, progress: null, cancellationToken).ConfigureAwait(false);
                assembled.Position = 0;
                return new AudioStreamResult
                {
                    Stream = assembled,
                    TotalBytes = null, // unknown until downloaded
                    SuggestedExtension = manifest.FileExtension?.TrimStart('.') ?? "flac"
                };
            }

            // Fallback to stream info
            var info = await _streamService.GetStreamInfoAsync(trackId, tidalQuality).ConfigureAwait(false);
            var ms = await _chunkDownloader.DownloadAndAssembleAsync(info, progress: null).ConfigureAwait(false);
            return new AudioStreamResult
            {
                Stream = ms,
                TotalBytes = null,
                SuggestedExtension = info.FileExtension?.TrimStart('.') ?? "flac"
            };
        }
    }
}
