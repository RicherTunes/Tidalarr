using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Tidalarr.Infrastructure.Http;

/// <summary>
/// Ensures gzip-compressed responses are transparently decompressed even when the server omits the Content-Encoding header.
/// </summary>
public sealed class GzipSniffingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.Content == null)
        {
            return response;
        }

        // If the server reported a content encoding, the primary handler already handled decompression.
        if (response.Content.Headers.ContentEncoding is { Count: > 0 })
        {
            return response;
        }

        var (bufferedStream, originalContent) = await BufferContentStreamAsync(response.Content, cancellationToken).ConfigureAwait(false);
        if (bufferedStream == null)
        {
            return response;
        }

        // Peek the gzip signature (0x1F8B)
        if (!IsGzipStream(bufferedStream))
        {
            RestoreContent(response, originalContent, bufferedStream);
            return response;
        }

        // Inflate and replace the content with the decompressed payload
        var decompressed = new MemoryStream();
        using (var gzip = new GZipStream(bufferedStream, CompressionMode.Decompress, leaveOpen: false))
        {
            await gzip.CopyToAsync(decompressed, cancellationToken).ConfigureAwait(false);
        }

        decompressed.Position = 0;
        bufferedStream.Dispose();

        var inflatedContent = new StreamContent(decompressed);
        CopyContentHeaders(originalContent, inflatedContent);

        if (inflatedContent.Headers.ContentType == null)
        {
            inflatedContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        var previousContent = response.Content;
        response.Content = inflatedContent;
        previousContent.Dispose();
        return response;
    }

    private static bool IsGzipStream(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 2)
        {
            return false;
        }

        var originalPosition = stream.Position;
        int first = stream.ReadByte();
        int second = stream.ReadByte();
        stream.Position = originalPosition;
        return first == 0x1F && second == 0x8B;
    }

    private static async Task<(Stream? bufferedStream, HttpContent originalContent)> BufferContentStreamAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        if (stream.CanSeek)
        {
            stream.Position = 0;
            return (stream, content);
        }

        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return (buffer, content);
    }

    private static void RestoreContent(HttpResponseMessage response, HttpContent originalContent, Stream bufferedStream)
    {
        if (bufferedStream.CanSeek)
        {
            bufferedStream.Position = 0;
        }

        if (!ReferenceEquals(response.Content, originalContent))
        {
            response.Content = originalContent;
            return;
        }

        // We consumed bytes for inspection; replace with a new content wrapper.
        var replacement = new StreamContent(bufferedStream);
        CopyContentHeaders(originalContent, replacement);
        response.Content = replacement;
    }

    private static void CopyContentHeaders(HttpContent source, HttpContent destination)
    {
        foreach (var header in source.Headers)
        {
            if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "Content-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            destination.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
