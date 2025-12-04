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
            byte[] bytes = Convert.FromBase64String(encodedManifest);
            string decoded = Encoding.UTF8.GetString(bytes);
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
        XDocument doc = XDocument.Parse(xmlContent);
        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        XElement adaptationSet = doc.Descendants(ns + "AdaptationSet").FirstOrDefault()
            ?? throw new InvalidOperationException("No AdaptationSet found in DASH manifest");

        string codec = adaptationSet.Attribute("codecs")?.Value ?? "unknown";
        int sampleRate = int.TryParse(adaptationSet.Attribute("audioSamplingRate")?.Value, out int rate) ? rate : 44100;
        string[] chunkUrls = ExtractChunkUrlsFromDash(adaptationSet, ns);
        string fileExt = DetermineFileExtension(codec, chunkUrls.FirstOrDefault() ?? string.Empty);

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
        using JsonDocument doc = JsonDocument.Parse(jsonContent);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("urls", out JsonElement urlsElement) || urlsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("No URLs found in BTS manifest");
        }

        string[] urls = urlsElement
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        if (urls.Length == 0)
        {
            throw new InvalidOperationException("No URLs found in BTS manifest");
        }

        string codec = root.TryGetProperty("codecs", out JsonElement c) ? c.GetString() ?? "unknown" : "unknown";
        string mimeType = root.TryGetProperty("mimeType", out JsonElement m) ? m.GetString() ?? "audio/unknown" : "audio/unknown";
        string encType = root.TryGetProperty("encryptionType", out JsonElement e2) ? e2.GetString() ?? "NONE" : "NONE";
        string? keyId = root.TryGetProperty("keyId", out JsonElement kidElement) ? kidElement.GetString() : null;
        string? securityToken = ExtractSecurityToken(root);
        int sampleRate = ExtractSampleRate(root);
        string fileExt = DetermineFileExtension(codec, urls[0]);
        bool isEncrypted = !string.Equals(encType, "NONE", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(securityToken);

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
        if (root.TryGetProperty("securityToken", out JsonElement tokenElement))
        {
            string? token = tokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        if (root.TryGetProperty("encryptionKey", out JsonElement encryptionKeyElement))
        {
            string? token = encryptionKeyElement.GetString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        if (root.TryGetProperty("drmSecurityToken", out JsonElement drmTokenElement))
        {
            string? token = drmTokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private static int ExtractSampleRate(JsonElement root)
    {
        if (root.TryGetProperty("sampleRate", out JsonElement sampleRateElement))
        {
            if (sampleRateElement.ValueKind == JsonValueKind.Number && sampleRateElement.TryGetInt32(out int sr))
            {
                return sr;
            }

            if (sampleRateElement.ValueKind == JsonValueKind.String && int.TryParse(sampleRateElement.GetString(), out int srFromString))
            {
                return srFromString;
            }
        }

        return 44100;
    }

    private string[] ExtractChunkUrlsFromDash(XElement adaptationSet, XNamespace ns)
    {
        XElement? representation = adaptationSet.Descendants(ns + "Representation").FirstOrDefault();
        string representationId = representation?.Attribute("id")?.Value ?? string.Empty;
        XElement? template = representation?.Element(ns + "SegmentTemplate") ?? adaptationSet.Descendants(ns + "SegmentTemplate").FirstOrDefault();
        string? mediaTemplate = template?.Attribute("media")?.Value;
        if (string.IsNullOrEmpty(mediaTemplate))
        {
            string[] mediaTemplates = adaptationSet.Descendants(ns + "SegmentTemplate")
                .Select(st => st.Attribute("media")?.Value)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToArray();
            return mediaTemplates.Any() ? mediaTemplates : [];
        }

        List<string> urls = [];

        string? initializationTemplate = template?.Attribute("initialization")?.Value;
        if (!string.IsNullOrEmpty(initializationTemplate))
        {
            string initUrl = initializationTemplate
                .Replace("$RepresentationID$", representationId)
                .Replace("$Number$", "0")
                .Replace("$Number%06d$", "000000");
            urls.Add(initUrl);
        }

        XElement? timeline = template?.Element(ns + "SegmentTimeline") ?? adaptationSet.Descendants(ns + "SegmentTimeline").FirstOrDefault();
        if (timeline == null)
        {
            string singleUrl = mediaTemplate
                .Replace("$RepresentationID$", representationId)
                .Replace("$Number$", "1")
                .Replace("$Number%06d$", "000001");
            urls.Add(singleUrl);
            return [.. urls];
        }

        IEnumerable<XElement> segments = timeline.Descendants(ns + "S");
        int number = 1;
        foreach (XElement s in segments)
        {
            int repeat = int.TryParse(s.Attribute("r")?.Value, out int r) ? r + 1 : 1;
            for (int i = 0; i < repeat; i++)
            {
                string url = mediaTemplate
                    .Replace("$Number$", number.ToString())
                    .Replace("$Number%06d$", number.ToString("D6"))
                    .Replace("$RepresentationID$", representationId);
                urls.Add(url);
                number++;
            }
        }

        return [.. urls];
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

