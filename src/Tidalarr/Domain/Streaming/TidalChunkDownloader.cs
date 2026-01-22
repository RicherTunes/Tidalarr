using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class ChunkDownloadProgress
{
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; }
    public double ProgressPercentage => TotalChunks > 0 ? (double)CompletedChunks / TotalChunks * 100 : 0;
}

public class TidalChunkDownloader(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    private const int ChunkBufferSize = 65536;

    /// <summary>
    /// Download and assemble chunks from Tidal DASH manifest.
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
        MemoryStream outputStream = new();
        int totalChunks = manifest.ChunkUrls.Length;
        int completedChunks = 0;

        foreach (string chunkUrl in manifest.ChunkUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using HttpRequestMessage req = new(HttpMethod.Get, chunkUrl);
                using HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(
                    req,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken: cancellationToken);
                _ = response.EnsureSuccessStatusCode();

                // Stream directly to output instead of buffering entire chunk in memory
                await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await contentStream.CopyToAsync(outputStream, cancellationToken);

                completedChunks++;
                progress?.Report(new ChunkDownloadProgress
                {
                    TotalChunks = totalChunks,
                    CompletedChunks = completedChunks
                });

                // Apply configurable delay (0 = no delay)
                if (chunkDelayMs > 0)
                {
                    await Task.Delay(chunkDelayMs, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                outputStream.Dispose();
                throw new InvalidOperationException($"Failed to download chunk {chunkUrl}: {ex.Message}", ex);
            }
        }

        outputStream.Position = 0;

        if (manifest.IsEncrypted && string.IsNullOrWhiteSpace(manifest.SecurityToken))
        {
            outputStream.Dispose();
            throw new InvalidOperationException("Encrypted manifest missing security token for decryption.");
        }

        if (RequiresDecryption(manifest.IsEncrypted, manifest.SecurityToken))
        {
            try
            {
                byte[] decrypted = TidalStreamDecryptor.Decrypt(outputStream.ToArray(), manifest.SecurityToken!);
                outputStream.Dispose();
                return new MemoryStream(decrypted, writable: false);
            }
            catch
            {
                outputStream.Dispose();
                throw;
            }
        }

        return outputStream;
    }

    /// <summary>
    /// File-backed variant of <see cref="DownloadAndAssembleAsync(TidalManifest,int,System.IProgress{ChunkDownloadProgress}?,System.Threading.CancellationToken)"/>
    /// intended for use by streaming orchestrators to avoid assembling full tracks in memory.
    /// </summary>
    public async Task<Stream> DownloadAndAssembleToFileStreamAsync(
        TidalManifest manifest,
        int chunkDelayMs = 0,
        int maxConcurrentChunkDownloads = 1,
        IProgress<ChunkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempFilePath = Path.GetTempFileName();
        FileStream fileStream = new(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, ChunkBufferSize, FileOptions.DeleteOnClose);

        try
        {
            int totalChunks = manifest.ChunkUrls.Length;
            int completedChunks = 0;

            maxConcurrentChunkDownloads = Math.Max(1, maxConcurrentChunkDownloads);
            if (chunkDelayMs > 0)
            {
                // Preserve existing "delay between chunk requests" behavior by disabling parallel chunk downloads.
                maxConcurrentChunkDownloads = 1;
            }

            if (maxConcurrentChunkDownloads <= 1 || manifest.ChunkUrls.Length <= 1)
            {
                for (int i = 0; i < manifest.ChunkUrls.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string chunkUrl = manifest.ChunkUrls[i];

                    using HttpRequestMessage req = new(HttpMethod.Get, chunkUrl);
                    using HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(
                        req,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken);
                    _ = response.EnsureSuccessStatusCode();

                    await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await contentStream.CopyToAsync(fileStream, cancellationToken);

                    completedChunks++;
                    progress?.Report(new ChunkDownloadProgress
                    {
                        TotalChunks = totalChunks,
                        CompletedChunks = completedChunks
                    });

                    if (chunkDelayMs > 0)
                    {
                        await Task.Delay(chunkDelayMs, cancellationToken);
                    }
                }
            }
            else
            {
                string chunkDir = Path.Combine(Path.GetTempPath(), $"tidalarr_chunks_{Guid.NewGuid():N}");
                Directory.CreateDirectory(chunkDir);

                var chunkPaths = new string[manifest.ChunkUrls.Length];
                var tasks = new Task[manifest.ChunkUrls.Length];
                var semaphore = new SemaphoreSlim(maxConcurrentChunkDownloads, maxConcurrentChunkDownloads);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                try
                {
                    for (int i = 0; i < manifest.ChunkUrls.Length; i++)
                    {
                        int index = i;
                        string url = manifest.ChunkUrls[index];
                        string path = Path.Combine(chunkDir, $"{index:D6}.chunk");
                        chunkPaths[index] = path;
                        tasks[index] = DownloadChunkToFileAsync(url, path, semaphore, cts.Token);
                    }

                    for (int i = 0; i < tasks.Length; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await tasks[i].ConfigureAwait(false);

                        await using (FileStream chunkStream = new(chunkPaths[i], FileMode.Open, FileAccess.Read, FileShare.Read, ChunkBufferSize, useAsync: true))
                        {
                            await chunkStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                        }

                        try { File.Delete(chunkPaths[i]); } catch { /* best effort */ }

                        completedChunks++;
                        progress?.Report(new ChunkDownloadProgress
                        {
                            TotalChunks = totalChunks,
                            CompletedChunks = completedChunks
                        });
                    }
                }
                catch
                {
                    try { cts.Cancel(); } catch { /* best effort */ }

                    // Best-effort: observe task completion to avoid unobserved exceptions.
                    foreach (Task t in tasks)
                    {
                        try { await t.ConfigureAwait(false); } catch { }
                    }

                    throw;
                }
                finally
                {
                    semaphore.Dispose();
                    try { Directory.Delete(chunkDir, recursive: true); } catch { /* best effort */ }
                }
            }

            if (manifest.IsEncrypted && string.IsNullOrWhiteSpace(manifest.SecurityToken))
            {
                throw new InvalidOperationException("Encrypted manifest missing security token for decryption.");
            }

            if (RequiresDecryption(manifest.IsEncrypted, manifest.SecurityToken))
            {
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await TidalStreamDecryptor.DecryptFileStreamAsync(fileStream, manifest.SecurityToken!).ConfigureAwait(false);
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

    /// <summary>
    /// Legacy method for backward compatibility with existing TidalStreamInfo.
    /// </summary>
    /// <param name="streamInfo">The stream info containing chunk URLs.</param>
    /// <param name="chunkDelayMs">Delay between chunk downloads in milliseconds. Use 0 for no delay.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Stream> DownloadAndAssembleAsync(
        TidalStreamInfo streamInfo,
        int chunkDelayMs = 0,
        int maxConcurrentChunkDownloads = 1,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempFilePath = Path.GetTempFileName();
        FileStream fileStream = new(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, ChunkBufferSize, FileOptions.DeleteOnClose);

        try
        {
            maxConcurrentChunkDownloads = Math.Max(1, maxConcurrentChunkDownloads);
            if (chunkDelayMs > 0)
            {
                maxConcurrentChunkDownloads = 1;
            }

            if (maxConcurrentChunkDownloads <= 1 || streamInfo.ChunkUrls.Length <= 1)
            {
                for (int i = 0; i < streamInfo.ChunkUrls.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string chunkUrl = streamInfo.ChunkUrls[i];

                    using HttpRequestMessage req = new(HttpMethod.Get, chunkUrl);
                    using HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(
                        req,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken);
                    _ = response.EnsureSuccessStatusCode();

                    await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await contentStream.CopyToAsync(fileStream, cancellationToken);

                    progress?.Report(i + 1);

                    if (chunkDelayMs > 0)
                    {
                        await Task.Delay(chunkDelayMs, cancellationToken);
                    }
                }
            }
            else
            {
                string chunkDir = Path.Combine(Path.GetTempPath(), $"tidalarr_chunks_{Guid.NewGuid():N}");
                Directory.CreateDirectory(chunkDir);

                var chunkPaths = new string[streamInfo.ChunkUrls.Length];
                var tasks = new Task[streamInfo.ChunkUrls.Length];
                var semaphore = new SemaphoreSlim(maxConcurrentChunkDownloads, maxConcurrentChunkDownloads);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                try
                {
                    for (int i = 0; i < streamInfo.ChunkUrls.Length; i++)
                    {
                        int index = i;
                        string url = streamInfo.ChunkUrls[index];
                        string path = Path.Combine(chunkDir, $"{index:D6}.chunk");
                        chunkPaths[index] = path;
                        tasks[index] = DownloadChunkToFileAsync(url, path, semaphore, cts.Token);
                    }

                    for (int i = 0; i < tasks.Length; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await tasks[i].ConfigureAwait(false);

                        await using (FileStream chunkStream = new(chunkPaths[i], FileMode.Open, FileAccess.Read, FileShare.Read, ChunkBufferSize, useAsync: true))
                        {
                            await chunkStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                        }

                        try { File.Delete(chunkPaths[i]); } catch { /* best effort */ }

                        progress?.Report(i + 1);
                    }
                }
                catch
                {
                    try { cts.Cancel(); } catch { /* best effort */ }
                    foreach (Task t in tasks)
                    {
                        try { await t.ConfigureAwait(false); } catch { }
                    }
                    throw;
                }
                finally
                {
                    semaphore.Dispose();
                    try { Directory.Delete(chunkDir, recursive: true); } catch { /* best effort */ }
                }
            }

            if (streamInfo.IsEncrypted && string.IsNullOrWhiteSpace(streamInfo.SecurityToken))
            {
                throw new InvalidOperationException("Encrypted stream info missing security token for decryption.");
            }

            if (RequiresDecryption(streamInfo.IsEncrypted, streamInfo.SecurityToken))
            {
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await TidalStreamDecryptor.DecryptFileStreamAsync(fileStream, streamInfo.SecurityToken!).ConfigureAwait(false);
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

    private async Task DownloadChunkToFileAsync(string chunkUrl, string outputPath, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using HttpRequestMessage req = new(HttpMethod.Get, chunkUrl);
            using HttpResponseMessage response = await this._httpClient.ExecuteWithRetryAsync(
                req,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();

            await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using FileStream fs = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkBufferSize, useAsync: true);
            await contentStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
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
            cancellationToken: cancellationToken);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, cancellationToken);
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
