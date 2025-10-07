using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

namespace Tidalarr.Infrastructure.Http;

/// <summary>
/// Minimal diagnostic wiretap. Enabled via LIDARR_PLUGIN_HTTP_TAP=1/true/yes.
/// Logs request/response lines and small textual bodies. Redacts sensitive headers.
/// Internal and hidden; used until the shared handler is available in all targets.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class DiagnosticTapHandler : DelegatingHandler
{
    private const string EnvFlag = "LIDARR_PLUGIN_HTTP_TAP";
    private const int MaxLoggedBodyBytes = 2 * 1024; // 2 KiB

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var enabled = IsEnabled();
        if (!enabled)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var id = CreateId();
        try
        {
            Console.WriteLine($"[http:{id}] -> {request.Method} {SanitizeUri(request.RequestUri)}");
            LogHeaders(id, request.Headers, true);
            if (request.Content != null)
            {
                LogHeaders(id, request.Content.Headers, true);
            }

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            Console.WriteLine($"[http:{id}] <- {(int)response.StatusCode} {response.ReasonPhrase}");
            LogHeaders(id, response.Headers, false);
            if (response.Content != null && IsTextual(response.Content.Headers))
            {
                try
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    var toLog = bytes.Length > MaxLoggedBodyBytes ? bytes.Take(MaxLoggedBodyBytes).ToArray() : bytes;
                    var charset = response.Content.Headers.ContentType?.CharSet;
                    var encoding = TryGetEncoding(charset) ?? Encoding.UTF8;
                    var snippet = SafeGetString(encoding, toLog);
                    Console.WriteLine($"[http:{id}] body: {snippet}{(bytes.Length > MaxLoggedBodyBytes ? " [truncated]" : string.Empty)}");

                    var clone = new ByteArrayContent(bytes);
                    CopyHeaders(response.Content.Headers, clone.Headers);
                    response.Content = clone;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[http:{id}] body-log failed: {ex.Message}");
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[http:{id}] exception: {ex.Message}");
            throw;
        }
    }

    private static bool IsEnabled()
    {
        var v = Environment.GetEnvironmentVariable(EnvFlag);
        if (string.IsNullOrWhiteSpace(v)) return false;
        v = v.Trim().ToLowerInvariant();
        return v is "1" or "true" or "yes";
    }

    private static string CreateId()
    {
        var g = Guid.NewGuid().ToString("N");
        return g.Substring(0, 6);
    }

    private static void LogHeaders(string id, HttpHeaders headers, bool request)
    {
        if (headers == null) return;
        foreach (var kv in headers)
        {
            if (IsSensitiveHeader(kv.Key)) continue;
            foreach (var v in kv.Value)
            {
                Console.WriteLine($"[http:{id}] {(request ? "req" : "rsp")} {kv.Key}: {v}");
            }
        }
    }

    private static bool IsSensitiveHeader(string name)
        => name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("-Authorization", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("-Signature", StringComparison.OrdinalIgnoreCase);

    private static bool IsTextual(HttpContentHeaders? headers)
    {
        var mt = headers?.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mt)) return false;
        if (mt.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return true;
        if (mt.Equals("application/json", StringComparison.OrdinalIgnoreCase)) return true;
        if (mt.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void CopyHeaders(HttpContentHeaders from, HttpContentHeaders to)
    {
        foreach (var h in from)
        {
            if (string.Equals(h.Key, "Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
            to.TryAddWithoutValidation(h.Key, h.Value);
        }
    }

    private static string SanitizeUri(Uri? uri)
    {
        if (uri == null) return string.Empty;
        if (string.IsNullOrEmpty(uri.Query)) return uri.ToString();
        try
        {
            var query = uri.Query.TrimStart('?');
            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                var idx = p.IndexOf('=');
                var key = idx >= 0 ? p[..idx] : p;
                var val = idx >= 0 ? p[(idx + 1)..] : string.Empty;
                if (IsSensitiveKey(Uri.UnescapeDataString(key))) val = "REDACTED";
                if (sb.Length > 0) sb.Append('&');
                sb.Append(key);
                if (idx >= 0) { sb.Append('='); sb.Append(val); }
            }
            var ub = new UriBuilder(uri) { Query = sb.ToString() };
            return ub.Uri.ToString();
        }
        catch { return uri.ToString(); }
    }

    private static bool IsSensitiveKey(string key)
        => key.Contains("token", StringComparison.OrdinalIgnoreCase)
        || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("code", StringComparison.OrdinalIgnoreCase)
        || key.Contains("key", StringComparison.OrdinalIgnoreCase);

    private static Encoding? TryGetEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return null;
        try { return Encoding.GetEncoding(charset); } catch { return null; }
    }

    private static string SafeGetString(Encoding enc, byte[] bytes)
    {
        try { return enc.GetString(bytes); } catch { return Convert.ToBase64String(bytes); }
    }
}

