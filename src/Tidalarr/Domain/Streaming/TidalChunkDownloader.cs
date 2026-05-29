using Microsoft.Extensions.Logging;
using Lidarr.Plugin.Common.Services.Download;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class ChunkDownloadProgress
{
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; }
    public double ProgressPercentage => TotalChunks > 0 ? (double)CompletedChunks / TotalChunks * 100 : 0;
}

/// <summary>
/// Tidal-specific chunk downloader. The parallel-chunk + temp-file orchestration is
/// delegated to common's <see cref="ChunkedHttpAssembler"/> (Phase 5c). This class
/// retains only the Tidal-specific concerns: shaping <see cref="ChunkSpec"/> from
/// <see cref="TidalManifest"/> / <see cref="TidalStreamInfo"/>, decryption via
/// <see cref="TidalStreamDecryptor"/>, and the legacy public API surface that
/// existing consumers depend on.
/// </summary>
public class TidalChunkDownloader(HttpClient httpClient, ILogger<TidalChunkDownloader>? logger = null)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<TidalChunkDownloader>? _logger = logger;
    private readonly ChunkedHttpAssembler _assembler = new(httpClient, logger as ILogger<ChunkedHttpAssembler>);
    private const int ChunkBufferSize = 65536;

    /// <summary>
    /// Download and assemble chunks from a Tidal DASH manifest into a fully-buffered
    /// <see cref="MemoryStream"/>. Sequential by design (one chunk at a time) so this
    /// path remains compatible with the historical behavior contract.
    /// </summary>
    /// <param name="manifest">The manifest containing chunk URLs.</param>
    /// <param name="chunkDelayMs">Delay between chunk downloads in milliseconds. Use 0 for no delay.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<MemoryStream> DownloadAndAssembleAsync(
        TidalManifest manifest,
        int chunkDelayMs = 0,
        IProgress<ChunkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (manifest.ChunkUrls.Length == 0)
            throw new InvalidOperationException("Manifest contains no chunk URLs — cannot assemble an empty stream.");

        if (manifest.IsEncrypted && string.IsNullOrWhiteSpace(manifest.SecurityToken))
        {
            throw new InvalidOperationException("Encrypted manifest missing security token for decryption.");
        }

        string outputPath = CreateTempOutputPath();

        try
        {
            await AssembleChunksToFileAsync(
                manifest.ChunkUrls,
                outputPath,
                chunkDelayMs,
                maxConcurrency: 1,
                progress: progress is null ? null : new ChunkDownloadProgressAdapter(progress),
                cancellationToken).ConfigureAwait(false);

            byte[] payload = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);

            if (RequiresDecryption(manifest.IsEncrypted, manifest.SecurityToken))
            {
                payload = TidalStreamDecryptor.Decrypt(payload, manifest.SecurityToken!);
            }

            return new MemoryStream(payload, writable: false);
        }
        catch (HttpRequestException ex) when (manifest.ChunkUrls.Length == 1)
        {
            // Preserve the historical "Failed to download chunk {url}" exception shape
            // for single-chunk callsites that downstream tests/validators rely on.
            throw new InvalidOperationException(
                $"Failed to download chunk {manifest.ChunkUrls[0]}: {ex.Message}", ex);
        }
        finally
        {
            TryDeleteFile(outputPath);
        }
    }

    /// <summary>
    /// File-backed variant of <see cref="DownloadAndAssembleAsync(TidalManifest,int,System.IProgress{ChunkDownloadProgress}?,CancellationToken)"/>
    /// intended for use by streaming orchestrators to avoid assembling full tracks in memory.
    /// </summary>
    public async Task<Stream> DownloadAndAssembleToFileStreamAsync(
        TidalManifest manifest,
        int chunkDelayMs = 0,
        int maxConcurrentChunkDownloads = 1,
        IProgress<ChunkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (manifest.ChunkUrls.Length == 0)
            throw new InvalidOperationException("Manifest contains no chunk URLs — cannot assemble an empty stream.");

        if (manifest.IsEncrypted && string.IsNullOrWhiteSpace(manifest.SecurityToken))
        {
            throw new InvalidOperationException("Encrypted manifest missing security token for decryption.");
        }

        return await DownloadAndAssembleToFileStreamCoreAsync(
            manifest.ChunkUrls,
            manifest.IsEncrypted,
            manifest.SecurityToken,
            chunkDelayMs,
            maxConcurrentChunkDownloads,
            progress is null ? null : new ChunkDownloadProgressAdapter(progress),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Legacy method for backward compatibility with existing TidalStreamInfo.
    /// </summary>
    /// <param name="streamInfo">The stream info containing chunk URLs.</param>
    /// <param name="chunkDelayMs">Delay between chunk downloads in milliseconds. Use 0 for no delay.</param>
    /// <param name="progress">Optional progress reporter (1-based completed-chunk count).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Stream> DownloadAndAssembleAsync(
        TidalStreamInfo streamInfo,
        int chunkDelayMs = 0,
        int maxConcurrentChunkDownloads = 1,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (streamInfo.IsEncrypted && string.IsNullOrWhiteSpace(streamInfo.SecurityToken))
        {
            throw new InvalidOperationException("Encrypted stream info missing security token for decryption.");
        }

        return await DownloadAndAssembleToFileStreamCoreAsync(
            streamInfo.ChunkUrls,
            streamInfo.IsEncrypted,
            streamInfo.SecurityToken,
            chunkDelayMs,
            maxConcurrentChunkDownloads,
            progress is null ? null : new ChunkCountProgressAdapter(progress),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> DownloadAndAssembleBytesAsync(
        TidalStreamInfo streamInfo,
        int chunkDelayMs = 0,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using Stream stream = await DownloadAndAssembleAsync(
            streamInfo,
            chunkDelayMs,
            maxConcurrentChunkDownloads: 1,
            progress: progress,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    public async Task<bool> ValidateChunkAccessibilityAsync(string[] chunkUrls, CancellationToken cancellationToken = default)
    {
        try
        {
            if (chunkUrls.Length == 0)
            {
                return false;
            }

            using HttpResponseMessage response = await this._httpClient.GetAsync(chunkUrls[0], HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validate chunk accessibility for new TidalStreamManifest format
    /// </summary>
    public async Task<bool> ValidateChunkAccessibilityAsync(TidalStreamManifest manifest, CancellationToken cancellationToken = default)
    {
        return await ValidateChunkAccessibilityAsync(manifest.ChunkUrls, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Stream> DownloadAndAssembleToFileStreamCoreAsync(
        string[] chunkUrls,
        bool isEncrypted,
        string? securityToken,
        int chunkDelayMs,
        int maxConcurrentChunkDownloads,
        IProgress<ChunkedAssemblyProgress>? progress,
        CancellationToken cancellationToken)
    {
        string outputPath = CreateTempOutputPath();

        try
        {
            await AssembleChunksToFileAsync(
                chunkUrls,
                outputPath,
                chunkDelayMs,
                maxConcurrentChunkDownloads,
                progress,
                cancellationToken).ConfigureAwait(false);

            // Open with DeleteOnClose so the temp file is reaped when the consumer
            // disposes the returned stream — preserves the historical contract from
            // the previous Path.GetTempFileName + DeleteOnClose path.
            FileStream fileStream = new(
                outputPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                ChunkBufferSize,
                FileOptions.DeleteOnClose);

            try
            {
                if (RequiresDecryption(isEncrypted, securityToken))
                {
                    await TidalStreamDecryptor.DecryptFileStreamAsync(fileStream, securityToken!, cancellationToken).ConfigureAwait(false);
                }

                _ = fileStream.Seek(0, SeekOrigin.Begin);
                return fileStream;
            }
            catch
            {
                fileStream.Dispose();
                throw;
            }
        }
        catch
        {
            TryDeleteFile(outputPath);
            throw;
        }
    }

    private Task AssembleChunksToFileAsync(
        string[] chunkUrls,
        string outputPath,
        int chunkDelayMs,
        int maxConcurrency,
        IProgress<ChunkedAssemblyProgress>? progress,
        CancellationToken cancellationToken)
    {
        ChunkSpec[] specs = new ChunkSpec[chunkUrls.Length];
        for (int i = 0; i < chunkUrls.Length; i++)
        {
            specs[i] = new ChunkSpec(i, chunkUrls[i]);
        }

        ChunkedAssemblyOptions options = new()
        {
            MaxConcurrency = Math.Max(1, maxConcurrency),
            ChunkDelay = chunkDelayMs > 0 ? TimeSpan.FromMilliseconds(chunkDelayMs) : TimeSpan.Zero,
            BufferSize = ChunkBufferSize,
            Progress = progress
        };

        return _assembler.AssembleAsync(specs, outputPath, options, cancellationToken);
    }

    private static string CreateTempOutputPath()
    {
        return Path.Combine(Path.GetTempPath(), $"tidalarr_chunks_{Guid.NewGuid():N}.bin");
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Best-effort cleanup of {Path} failed", path);
        }
    }

    private static bool RequiresDecryption(bool isEncrypted, string? securityToken)
    {
        return isEncrypted && !string.IsNullOrWhiteSpace(securityToken);
    }

    /// <summary>Adapter from common's <see cref="ChunkedAssemblyProgress"/> to the plugin's <see cref="ChunkDownloadProgress"/>.</summary>
    private sealed class ChunkDownloadProgressAdapter(IProgress<ChunkDownloadProgress> inner) : IProgress<ChunkedAssemblyProgress>
    {
        private readonly IProgress<ChunkDownloadProgress> _inner = inner;
        public void Report(ChunkedAssemblyProgress value)
        {
            _inner.Report(new ChunkDownloadProgress
            {
                TotalChunks = value.TotalChunks,
                CompletedChunks = value.CompletedChunks
            });
        }
    }

    /// <summary>Adapter from common's <see cref="ChunkedAssemblyProgress"/> to a 1-based chunk-count <see cref="IProgress{Int32}"/>.</summary>
    private sealed class ChunkCountProgressAdapter(IProgress<int> inner) : IProgress<ChunkedAssemblyProgress>
    {
        private readonly IProgress<int> _inner = inner;
        public void Report(ChunkedAssemblyProgress value) => _inner.Report(value.CompletedChunks);
    }
}
