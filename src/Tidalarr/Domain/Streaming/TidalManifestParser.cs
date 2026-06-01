using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Lidarr.Plugin.Common.Services.Streaming.Manifests;
using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Streaming;

public class TidalManifestParser
{
    // Shared, spec-correct MPEG-DASH segment-index parser (Common). Replaces tidalarr's former
    // in-house DASH walk, which hardcoded the first $Number$ to 1 and ignored SegmentTemplate
    // @startNumber (an off-by-one for any manifest whose first segment number is not 1).
    private static readonly DashManifestParser DashParser = new();

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

        XElement? representation = adaptationSet.Descendants(ns + "Representation").FirstOrDefault();
        string codecRaw =
            representation?.Attribute("codecs")?.Value ??
            adaptationSet.Attribute("codecs")?.Value ??
            "unknown";
        string codec = NormalizeCodec(codecRaw);
        int sampleRate = int.TryParse(adaptationSet.Attribute("audioSamplingRate")?.Value, out int rate) ? rate : 44100;

        // Segment index resolution (init + ordered media segments) is delegated to Common's
        // spec-correct parser, which honors @startNumber. Tidal manifests carry only absolute
        // segment URLs, so no base URL is required.
        string[] chunkUrls = ExtractChunkUrlsFromDash(adaptationSet, ns, xmlContent);

        // Tidal DASH streams are delivered in an MP4/M4A container (even when the codec inside is FLAC).
        // Keep the container extension stable so downstream post-processing can safely extract/remux later.
        string fileExt = ".m4a";

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

        string[] urls = [.. urlsElement
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))];

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

    private static string[] ExtractChunkUrlsFromDash(XElement adaptationSet, XNamespace ns, string xmlContent)
    {
        // Delegate the spec-correct walk (init segment at index 0, then @startNumber-based
        // $Number$ numbering with SegmentTimeline r=+1 repeat semantics) to Common.
        StreamManifest parsed = DashParser.Parse(xmlContent, string.Empty);
        if (parsed.Segments.Count > 0)
        {
            return [.. parsed.Segments.Select(s => s.Url)];
        }

        // Degenerate Tidal manifest: a SegmentTemplate@media with neither a SegmentTimeline nor
        // an @duration yields no media segments under the spec parser. Preserve tidalarr's
        // historical single-segment fallback (one media URL at @startNumber, default 1), using the
        // same numbering rules so behavior matches the spec parser had a timeline been present.
        XElement? representation = adaptationSet.Descendants(ns + "Representation").FirstOrDefault();
        string representationId = representation?.Attribute("id")?.Value ?? string.Empty;
        XElement? template = representation?.Element(ns + "SegmentTemplate")
            ?? adaptationSet.Descendants(ns + "SegmentTemplate").FirstOrDefault();
        string? mediaTemplate = template?.Attribute("media")?.Value;
        if (template is null || string.IsNullOrEmpty(mediaTemplate))
        {
            return [];
        }

        XElement? timeline = template.Element(ns + "SegmentTimeline")
            ?? adaptationSet.Descendants(ns + "SegmentTimeline").FirstOrDefault();
        if (timeline != null)
        {
            // A timeline was present but produced no segments (e.g. all <S> empty) — nothing to emit.
            return [];
        }

        uint startNumber = uint.TryParse(template.Attribute("startNumber")?.Value, out uint sn) ? sn : 1;
        string singleUrl = mediaTemplate
            .Replace("$RepresentationID$", representationId)
            .Replace("$Number%06d$", startNumber.ToString("D6"))
            .Replace("$Number$", startNumber.ToString());
        return [singleUrl];
    }

    private string DetermineFileExtension(string codec, string sampleUrl)
    {
        if (codec.Contains("flac", StringComparison.OrdinalIgnoreCase))
        {
            return ".flac";
        }

        if (codec.Contains("mp4a", StringComparison.OrdinalIgnoreCase))
        {
            return ".m4a";
        }

        if (!string.IsNullOrEmpty(sampleUrl))
        {
            if (sampleUrl.Contains(".flac", StringComparison.OrdinalIgnoreCase))
            {
                return ".flac";
            }

            if (sampleUrl.Contains(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                return ".m4a";
            }

            if (sampleUrl.Contains(".ts", StringComparison.OrdinalIgnoreCase))
            {
                return ".ts";
            }
        }
        return ".m4a";
    }

    private static string NormalizeCodec(string codecRaw)
    {
        return codecRaw.Contains("flac", StringComparison.OrdinalIgnoreCase)
            ? "FLAC"
            : codecRaw.Contains("mp4a", StringComparison.OrdinalIgnoreCase) ? "MP4A" : codecRaw.Trim();
    }
}
