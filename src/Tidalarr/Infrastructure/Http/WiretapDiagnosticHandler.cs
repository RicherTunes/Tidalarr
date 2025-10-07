using System.Net.Http.Headers;

namespace Tidalarr.Infrastructure.Http;

/// <summary>
/// Lightweight console logger that can be enabled via the TIDALARR_HTTP_TRACE environment variable.
/// Adds per-request diagnostics (status, content headers, and first few payload bytes).
/// </summary>
public sealed class WiretapDiagnosticHandler : DelegatingHandler
{
    private const string TraceFlag = "TIDALARR_HTTP_TRACE";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!IsEnabled())
        {
            return response;
        }

        try
        {
            var id = Guid.NewGuid().ToString("N")[..8];
            Console.WriteLine($"[HTTP:{id}] {request.Method} {request.RequestUri}");
            Console.WriteLine($"[HTTP:{id}] -> Status {(int)response.StatusCode} {response.StatusCode}");

            if (response.Content != null)
            {
                var encoding = string.Join(",", response.Content.Headers.ContentEncoding ?? Array.Empty<string>());
                var mediaType = response.Content.Headers.ContentType?.ToString() ?? "(none)";
                Console.WriteLine($"[HTTP:{id}] -> Content-Type={mediaType} Content-Encoding={(string.IsNullOrEmpty(encoding) ? "(none)" : encoding)}");

                var buffer = await BufferContentAsync(response.Content, cancellationToken).ConfigureAwait(false);
                var preview = string.Join("-", buffer.preview.Select(b => b.ToString("X2")));
                Console.WriteLine($"[HTTP:{id}] -> FirstBytes={preview}");

                if (buffer.replacement != null)
                {
                    response.Content = buffer.replacement;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HTTP] WiretapDiagnosticHandler failed: {ex.Message}");
        }

        return response;
    }

    private static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable(TraceFlag), "1", StringComparison.OrdinalIgnoreCase);

    private static async Task<(byte[] preview, HttpContent? replacement)> BufferContentAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var replacement = default(HttpContent);

        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            replacement = new StreamContent(buffer);
            CopyHeaders(content, replacement);
            stream = buffer;
        }
        else
        {
            stream.Position = 0;
        }

        var preview = new byte[Math.Min(16, stream.Length)];
        _ = await stream.ReadAsync(preview, 0, preview.Length, cancellationToken).ConfigureAwait(false);
        stream.Position = 0;

        return (preview, replacement);
    }

    private static void CopyHeaders(HttpContent source, HttpContent destination)
    {
        foreach (var header in source.Headers)
        {
            destination.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}


