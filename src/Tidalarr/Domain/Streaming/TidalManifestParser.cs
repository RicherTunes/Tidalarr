using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class TidalManifestParser
{
    public TidalManifest ParseManifest(string encodedManifest, string mimeType)
    {
        try
        {
            var bytes = Convert.FromBase64String(encodedManifest);
            var decoded = Encoding.UTF8.GetString(bytes);
            return mimeType switch
            {
                "application/dash+xml" => ParseDashManifest(decoded),
                "application/vnd.tidal.bts" => ParseBtsManifest(decoded),
                _ => throw new NotSupportedException($"Unsupported manifest type: {mimeType}")
            };
        }
        catch (FormatException)
        {
            throw new FormatException("Invalid base64 manifest encoding");
        }
    }

    private TidalManifest ParseDashManifest(string xmlContent)
    {
        var doc = XDocument.Parse(xmlContent);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var adaptationSet = doc.Descendants(ns + "AdaptationSet").FirstOrDefault()
            ?? throw new InvalidOperationException("No AdaptationSet found in DASH manifest");

        var codec = adaptationSet.Attribute("codecs")?.Value ?? "unknown";
        var sampleRate = int.TryParse(adaptationSet.Attribute("audioSamplingRate")?.Value, out var rate) ? rate : 44100;
        var chunkUrls = ExtractChunkUrlsFromDash(adaptationSet, ns);
        var fileExt = DetermineFileExtension(codec, chunkUrls.FirstOrDefault() ?? string.Empty);

        return new TidalManifest(
            ChunkUrls: chunkUrls,
            Codec: codec,
            MimeType: "application/dash+xml",
            FileExtension: fileExt,
            SampleRate: sampleRate,
            IsEncrypted: false,
            KeyId: null,
            SecurityToken: null);
    }

    private TidalManifest ParseBtsManifest(string jsonContent)
    {
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        if (!root.TryGetProperty("urls", out var urlsElement) || urlsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("No URLs found in BTS manifest");
        }

        var urls = urlsElement
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        if (urls.Length == 0)
        {
            throw new InvalidOperationException("No URLs found in BTS manifest");
        }

        var codec = root.TryGetProperty("codecs", out var c) ? c.GetString() ?? "unknown" : "unknown";
        var mimeType = root.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "audio/unknown" : "audio/unknown";
        var encType = root.TryGetProperty("encryptionType", out var e2) ? e2.GetString() ?? "NONE" : "NONE";
        var keyId = root.TryGetProperty("keyId", out var kidElement) ? kidElement.GetString() : null;
        var securityToken = ExtractSecurityToken(root);
        var sampleRate = ExtractSampleRate(root);
        var fileExt = DetermineFileExtension(codec, urls[0]);
        var isEncrypted = !string.Equals(encType, "NONE", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(securityToken);

        return new TidalManifest(
            ChunkUrls: urls,
            Codec: codec,
            MimeType: mimeType,
            FileExtension: fileExt,
            SampleRate: sampleRate,
            IsEncrypted: isEncrypted,
            KeyId: keyId,
            SecurityToken: securityToken);
    }

    private static string? ExtractSecurityToken(JsonElement root)
    {
        if (root.TryGetProperty("securityToken", out var tokenElement))
        {
            var token = tokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        if (root.TryGetProperty("encryptionKey", out var encryptionKeyElement))
        {
            var token = encryptionKeyElement.GetString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        if (root.TryGetProperty("drmSecurityToken", out var drmTokenElement))
        {
            var token = drmTokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private static int ExtractSampleRate(JsonElement root)
    {
        if (root.TryGetProperty("sampleRate", out var sampleRateElement))
        {
            if (sampleRateElement.ValueKind == JsonValueKind.Number && sampleRateElement.TryGetInt32(out var sr))
            {
                return sr;
            }

            if (sampleRateElement.ValueKind == JsonValueKind.String && int.TryParse(sampleRateElement.GetString(), out var srFromString))
            {
                return srFromString;
            }
        }

        return 44100;
    }

    private string[] ExtractChunkUrlsFromDash(XElement adaptationSet, XNamespace ns)
    {
        var representation = adaptationSet.Descendants(ns + "Representation").FirstOrDefault();
        var representationId = representation?.Attribute("id")?.Value ?? string.Empty;
        var template = representation?.Element(ns + "SegmentTemplate") ?? adaptationSet.Descendants(ns + "SegmentTemplate").FirstOrDefault();
        var mediaTemplate = template?.Attribute("media")?.Value;
        if (string.IsNullOrEmpty(mediaTemplate))
        {
            var mediaTemplates = adaptationSet.Descendants(ns + "SegmentTemplate")
                .Select(st => st.Attribute("media")?.Value)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToArray();
            return mediaTemplates.Any() ? mediaTemplates : Array.Empty<string>();
        }

        var urls = new List<string>();

        var initializationTemplate = template?.Attribute("initialization")?.Value;
        if (!string.IsNullOrEmpty(initializationTemplate))
        {
            var initUrl = initializationTemplate
                .Replace("$RepresentationID$", representationId)
                .Replace("$Number$", "0")
                .Replace("$Number%06d$", "000000");
            urls.Add(initUrl);
        }

        var timeline = template?.Element(ns + "SegmentTimeline") ?? adaptationSet.Descendants(ns + "SegmentTimeline").FirstOrDefault();
        if (timeline == null)
        {
            var singleUrl = mediaTemplate
                .Replace("$RepresentationID$", representationId)
                .Replace("$Number$", "1")
                .Replace("$Number%06d$", "000001");
            urls.Add(singleUrl);
            return urls.ToArray();
        }

        var segments = timeline.Descendants(ns + "S");
        var number = 1;
        foreach (var s in segments)
        {
            var repeat = int.TryParse(s.Attribute("r")?.Value, out var r) ? r + 1 : 1;
            for (int i = 0; i < repeat; i++)
            {
                var url = mediaTemplate
                    .Replace("$Number$", number.ToString())
                    .Replace("$Number%06d$", number.ToString("D6"))
                    .Replace("$RepresentationID$", representationId);
                urls.Add(url);
                number++;
            }
        }

        return urls.ToArray();
    }

    private string DetermineFileExtension(string codec, string sampleUrl)
    {
        if (codec.Contains("flac", StringComparison.OrdinalIgnoreCase)) return ".flac";
        if (codec.Contains("mp4a", StringComparison.OrdinalIgnoreCase)) return ".m4a";
        if (!string.IsNullOrEmpty(sampleUrl))
        {
            if (sampleUrl.Contains(".flac", StringComparison.OrdinalIgnoreCase)) return ".flac";
            if (sampleUrl.Contains(".mp4", StringComparison.OrdinalIgnoreCase)) return ".m4a";
            if (sampleUrl.Contains(".ts", StringComparison.OrdinalIgnoreCase)) return ".ts";
        }
        return ".m4a";
    }
}

