using System;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class ChunkDownloadProgress
{
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; }
    public double ProgressPercentage => TotalChunks > 0 ? (double)CompletedChunks / TotalChunks * 100 : 0;
}

public class TidalChunkDownloader
{
    private readonly HttpClient _httpClient;

    public TidalChunkDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Download and assemble chunks from Tidal DASH manifest
    /// </summary>
    public async Task<MemoryStream> DownloadAndAssembleAsync(
        TidalManifest manifest,
        IProgress<ChunkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputStream = new MemoryStream();
        var totalChunks = manifest.ChunkUrls.Length;
        var completedChunks = 0;

        foreach (var chunkUrl in manifest.ChunkUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, chunkUrl);
                var response = await _httpClient.ExecuteWithResilienceAsync(req, cancellationToken: cancellationToken);
                response.EnsureSuccessStatusCode();

                var chunkData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                await outputStream.WriteAsync(chunkData, 0, chunkData.Length, cancellationToken);

                completedChunks++;
                progress?.Report(new ChunkDownloadProgress
                {
                    TotalChunks = totalChunks,
                    CompletedChunks = completedChunks
                });

                await Task.Delay(50, cancellationToken);
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
                var decrypted = TidalStreamDecryptor.Decrypt(outputStream.ToArray(), manifest.SecurityToken!);
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
    /// Legacy method for backward compatibility with existing TidalStreamInfo
    /// </summary>
    public async Task<Stream> DownloadAndAssembleAsync(TidalStreamInfo streamInfo, IProgress<int>? progress = null)
    {
        var tempFilePath = Path.GetTempFileName();
        var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.DeleteOnClose);

        try
        {
            for (int i = 0; i < streamInfo.ChunkUrls.Length; i++)
            {
                var chunkUrl = streamInfo.ChunkUrls[i];

                using var req = new HttpRequestMessage(HttpMethod.Get, chunkUrl);
                using var response = await _httpClient.ExecuteWithResilienceAsync(req);
                response.EnsureSuccessStatusCode();

                using var contentStream = await response.Content.ReadAsStreamAsync();
                await contentStream.CopyToAsync(fileStream);

                progress?.Report(i + 1);
            }

            if (streamInfo.IsEncrypted && string.IsNullOrWhiteSpace(streamInfo.SecurityToken))
            {
                throw new InvalidOperationException("Encrypted stream info missing security token for decryption.");
            }

            if (RequiresDecryption(streamInfo.IsEncrypted, streamInfo.SecurityToken))
            {
                await fileStream.FlushAsync().ConfigureAwait(false);
                await TidalStreamDecryptor.DecryptFileStreamAsync(fileStream, streamInfo.SecurityToken!).ConfigureAwait(false);
            }

            fileStream.Seek(0, SeekOrigin.Begin);
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
        await using var stream = await DownloadAndAssembleAsync(streamInfo, progress);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private async Task<byte[]> DownloadChunkWithRetryAsync(string chunkUrl, int maxRetries = 3)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await _httpClient.GetByteArrayAsync(chunkUrl);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)));
            }
        }

        throw new InvalidOperationException($"Failed to download chunk {chunkUrl} after {maxRetries} attempts", lastException);
    }

    public async Task<bool> ValidateChunkAccessibilityAsync(string[] chunkUrls)
    {
        try
        {
            if (chunkUrls.Length == 0)
                return false;

            using var response = await _httpClient.GetAsync(chunkUrls[0], HttpCompletionOption.ResponseHeadersRead);
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
        => isEncrypted && !string.IsNullOrWhiteSpace(securityToken);
}
