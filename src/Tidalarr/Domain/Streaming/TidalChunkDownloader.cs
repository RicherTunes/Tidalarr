using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class ChunkDownloadProgress
{
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; }
    public double ProgressPercentage => TotalChunks > 0 ? (double)CompletedChunks / TotalChunks * 100 : 0;
}

public class TidalChunkDownloader(HttpClient httpClient, int chunkDelayMs = 50)
{
    private const int MaxChunkDelayMs = 2000;

    private readonly HttpClient _httpClient = httpClient;
    private readonly int _chunkDelayMs = Math.Clamp(chunkDelayMs, 0, MaxChunkDelayMs);

    /// <summary>
    /// Delay in milliseconds between chunk downloads (0-2000ms). Set to 0 to disable.
    /// </summary>
    public int ChunkDelayMs => _chunkDelayMs;

    /// <summary>
    /// Download and assemble chunks from Tidal DASH manifest
    /// </summary>
    public async Task<MemoryStream> DownloadAndAssembleAsync(
        TidalManifest manifest,
        IProgress<ChunkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using Stream stream = await DownloadAndAssembleStreamAsync(manifest, progress, cancellationToken).ConfigureAwait(false);
        MemoryStream outputStream = new();
        await stream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
        outputStream.Position = 0;
        return outputStream;
    }

    /// <summary>
    /// Download and assemble chunks from Tidal DASH manifest into a disk-backed stream.
    /// </summary>
    public async Task<Stream> DownloadAndAssembleStreamAsync(
        TidalManifest manifest,
        IProgress<ChunkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempFilePath = Path.GetTempFileName();
        FileStream fileStream = new(
            tempFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            65536,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        try
        {
            int totalChunks = manifest.ChunkUrls.Length;
            int completedChunks = 0;

            foreach (string chunkUrl in manifest.ChunkUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using HttpRequestMessage req = new(HttpMethod.Get, chunkUrl);
                    using HttpResponseMessage response = await _httpClient
                        .ExecuteWithRetryAsync(req, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    _ = response.EnsureSuccessStatusCode();

                    await using Stream contentStream = await response.Content
                        .ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);
                    await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                    completedChunks++;
                    progress?.Report(new ChunkDownloadProgress
                    {
                        TotalChunks = totalChunks,
                        CompletedChunks = completedChunks
                    });

                    if (_chunkDelayMs > 0)
                    {
                        await Task.Delay(_chunkDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to download chunk {chunkUrl}: {ex.Message}", ex);
                }
            }

            if (manifest.IsEncrypted && string.IsNullOrWhiteSpace(manifest.SecurityToken))
            {
                throw new InvalidOperationException("Encrypted manifest missing security token for decryption.");
            }

            if (RequiresDecryption(manifest.IsEncrypted, manifest.SecurityToken))
            {
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await TidalStreamDecryptor
                    .DecryptFileStreamAsync(fileStream, manifest.SecurityToken!, cancellationToken)
                    .ConfigureAwait(false);
            }

            _ = fileStream.Seek(0, SeekOrigin.Begin);
            return fileStream;
        }
        catch
        {
            await fileStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility with existing TidalStreamInfo
    /// </summary>
    public async Task<Stream> DownloadAndAssembleAsync(
        TidalStreamInfo streamInfo,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempFilePath = Path.GetTempFileName();
        FileStream fileStream = new(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.DeleteOnClose);

        try
        {
            for (int i = 0; i < streamInfo.ChunkUrls.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string chunkUrl = streamInfo.ChunkUrls[i];

                using HttpRequestMessage req = new(HttpMethod.Get, chunkUrl);
                using HttpResponseMessage response = await _httpClient
                    .ExecuteWithRetryAsync(req, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();

                await using Stream contentStream = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                progress?.Report(i + 1);

                if (_chunkDelayMs > 0)
                {
                    await Task.Delay(_chunkDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }

            if (streamInfo.IsEncrypted && string.IsNullOrWhiteSpace(streamInfo.SecurityToken))
            {
                throw new InvalidOperationException("Encrypted stream info missing security token for decryption.");
            }

            if (RequiresDecryption(streamInfo.IsEncrypted, streamInfo.SecurityToken))
            {
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await TidalStreamDecryptor
                    .DecryptFileStreamAsync(fileStream, streamInfo.SecurityToken!, cancellationToken)
                    .ConfigureAwait(false);
            }

            _ = fileStream.Seek(0, SeekOrigin.Begin);
            return fileStream;
        }
        catch
        {
            fileStream?.Dispose();
            throw;
        }
    }

    public async Task<byte[]> DownloadAndAssembleBytesAsync(TidalStreamInfo streamInfo, IProgress<int>? progress = null)
    {
        await using Stream stream = await DownloadAndAssembleAsync(streamInfo, progress).ConfigureAwait(false);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public async Task<bool> ValidateChunkAccessibilityAsync(string[] chunkUrls)
    {
        try
        {
            if (chunkUrls.Length == 0)
                return false;

            using HttpResponseMessage response = await this._httpClient.GetAsync(chunkUrls[0], HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validate chunk accessibility for new StreamManifest format
    /// </summary>
    public async Task<bool> ValidateChunkAccessibilityAsync(StreamManifest manifest)
    {
        return await ValidateChunkAccessibilityAsync(manifest.ChunkUrls);
    }

    private static bool RequiresDecryption(bool isEncrypted, string? securityToken)
    {
        return isEncrypted && !string.IsNullOrWhiteSpace(securityToken);
    }
}
