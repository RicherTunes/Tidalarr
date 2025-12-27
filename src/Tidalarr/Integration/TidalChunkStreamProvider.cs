using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Abstractions.Models;
using Tidalarr.Core.Mappers;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Integration
{
    public class TidalChunkStreamProvider(TidalStreamService streamService, TidalChunkDownloader chunkDownloader, TidalModelMapper mapper) : IAudioStreamProvider
    {
        private readonly TidalStreamService _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
        private readonly TidalChunkDownloader _chunkDownloader = chunkDownloader ?? throw new ArgumentNullException(nameof(chunkDownloader));
        private readonly TidalModelMapper _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

        public async Task<AudioStreamResult> GetStreamAsync(string trackId, StreamingQuality? quality = null, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("[TidalChunkStreamProvider] GetStreamAsync for track: " + trackId);
            TidalQuality tidalQuality = quality != null ? this._mapper.FromStreamingQuality(quality) : TidalQuality.Lossless;

            TidalManifest? manifest = null;
            try
            {
                Console.WriteLine("[TidalChunkStreamProvider] Fetching manifest...");
                manifest = await this._streamService.GetParsedManifestAsync(trackId, tidalQuality).ConfigureAwait(false);
                int chunkCount = manifest?.ChunkUrls?.Length ?? 0;
                Console.WriteLine("[TidalChunkStreamProvider] Manifest: chunks=" + chunkCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TidalChunkStreamProvider] Manifest FAILED: " + ex.Message);
            }

            if (manifest != null && manifest.ChunkUrls?.Any() == true)
            {
                int chunks = manifest.ChunkUrls.Length;
                Console.WriteLine("[TidalChunkStreamProvider] Downloading " + chunks + " chunks...");
                MemoryStream assembled = await this._chunkDownloader.DownloadAndAssembleAsync(manifest, progress: null, cancellationToken).ConfigureAwait(false);
                assembled.Position = 0;
                Console.WriteLine("[TidalChunkStreamProvider] Assembled: " + assembled.Length + " bytes");
                return new AudioStreamResult
                {
                    Stream = assembled,
                    TotalBytes = null,
                    SuggestedExtension = manifest.FileExtension?.TrimStart('.') ?? "m4a"
                };
            }

            Console.WriteLine("[TidalChunkStreamProvider] Fallback to legacy stream info...");
            TidalStreamInfo info = await this._streamService.GetStreamInfoAsync(trackId, tidalQuality).ConfigureAwait(false);
            int legacyChunks = info.ChunkUrls?.Length ?? 0;
            Console.WriteLine("[TidalChunkStreamProvider] Legacy: chunks=" + legacyChunks);
            Stream ms = await this._chunkDownloader.DownloadAndAssembleAsync(info, progress: null).ConfigureAwait(false);
            long size = (ms as MemoryStream)?.Length ?? -1;
            Console.WriteLine("[TidalChunkStreamProvider] Legacy assembled: " + size + " bytes");
            return new AudioStreamResult
            {
                Stream = ms,
                TotalBytes = null,
                SuggestedExtension = info.FileExtension?.TrimStart('.') ?? "m4a"
            };
        }
    }
}
