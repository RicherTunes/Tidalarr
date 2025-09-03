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
            EncryptionKey: null);
    }

    private TidalManifest ParseBtsManifest(string jsonContent)
    {
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        var urls = root.GetProperty("urls").EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
        if (!urls.Any()) throw new InvalidOperationException("No URLs found in BTS manifest");

        var codec = root.TryGetProperty("codecs", out var c) ? c.GetString() ?? "unknown" : "unknown";
        var mimeType = root.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "audio/unknown" : "audio/unknown";
        var encType = root.TryGetProperty("encryptionType", out var e2) ? e2.GetString() ?? "NONE" : "NONE";
        var fileExt = DetermineFileExtension(codec, urls.First());

        return new TidalManifest(
            ChunkUrls: urls,
            Codec: codec,
            MimeType: mimeType,
            FileExtension: fileExt,
            SampleRate: 44100,
            IsEncrypted: !string.Equals(encType, "NONE", StringComparison.OrdinalIgnoreCase),
            EncryptionKey: null);
    }

    private string[] ExtractChunkUrlsFromDash(XElement adaptationSet, XNamespace ns)
    {
        var template = adaptationSet.Descendants(ns + "SegmentTemplate").FirstOrDefault();
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

        var timeline = adaptationSet.Descendants(ns + "SegmentTimeline").FirstOrDefault();
        if (timeline == null)
            return new[] { mediaTemplate! };

        var urls = new List<string>();
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
                    .Replace("$RepresentationID$", "audio_flac_44100_1411");
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
        if (sampleUrl.Contains(".flac", StringComparison.OrdinalIgnoreCase)) return ".flac";
        if (sampleUrl.Contains(".mp4", StringComparison.OrdinalIgnoreCase)) return ".m4a";
        if (sampleUrl.Contains(".ts", StringComparison.OrdinalIgnoreCase)) return ".ts";
        return ".m4a";
    }
}
