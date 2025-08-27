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
            var manifestData = Convert.FromBase64String(encodedManifest);
            var decodedManifest = Encoding.UTF8.GetString(manifestData);
            
            return mimeType switch
            {
                "application/dash+xml" => ParseDashManifest(decodedManifest),
                "application/vnd.tidal.bts" => ParseBtsManifest(decodedManifest),
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
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            
            var adaptationSet = doc.Descendants(ns + "AdaptationSet").FirstOrDefault()
                ?? throw new InvalidOperationException("No AdaptationSet found in DASH manifest");
            
            // Extract codec and mime type
            var codec = adaptationSet.Attribute("codecs")?.Value ?? "unknown";
            var mimeType = adaptationSet.Attribute("mimeType")?.Value ?? "audio/unknown";
            var sampleRate = int.TryParse(adaptationSet.Attribute("audioSamplingRate")?.Value, out var rate) ? rate : 44100;
            
            // Extract chunk URLs from segment template
            var chunkUrls = ExtractChunkUrlsFromDash(adaptationSet, ns);
            
            // Determine file extension from codec
            var fileExtension = DetermineFileExtension(codec, chunkUrls.FirstOrDefault() ?? "");
            
            return new TidalManifest(
                ChunkUrls: chunkUrls,
                Codec: codec,
                MimeType: "application/dash+xml",
                FileExtension: fileExtension,
                SampleRate: sampleRate,
                IsEncrypted: false, // TODO: Detect encryption from manifest
                EncryptionKey: null
            );
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to parse DASH manifest: {ex.Message}", ex);
        }
    }
    
    private TidalManifest ParseBtsManifest(string jsonContent)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            
            var urls = root.GetProperty("urls").EnumerateArray()
                .Select(url => url.GetString() ?? string.Empty)
                .Where(url => !string.IsNullOrEmpty(url))
                .ToArray();
                
            if (!urls.Any())
                throw new InvalidOperationException("No URLs found in BTS manifest");
            
            var codec = root.TryGetProperty("codecs", out var codecProp) ? codecProp.GetString() ?? "unknown" : "unknown";
            var mimeType = root.TryGetProperty("mimeType", out var mimeTypeProp) ? mimeTypeProp.GetString() ?? "audio/unknown" : "audio/unknown";
            var encryptionType = root.TryGetProperty("encryptionType", out var encProp) ? encProp.GetString() ?? "NONE" : "NONE";
            
            var fileExtension = DetermineFileExtension(codec, urls.First());
            
            return new TidalManifest(
                ChunkUrls: urls,
                Codec: codec,
                MimeType: mimeType,
                FileExtension: fileExtension,
                SampleRate: 44100, // Default for BTS
                IsEncrypted: encryptionType != "NONE",
                EncryptionKey: null // TODO: Extract encryption key if present
            );
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to parse BTS manifest: {ex.Message}", ex);
        }
    }
    
    private string[] ExtractChunkUrlsFromDash(XElement adaptationSet, XNamespace ns)
    {
        var segmentTemplate = adaptationSet.Descendants(ns + "SegmentTemplate").FirstOrDefault();
        if (segmentTemplate == null)
        {
            // Fallback: Look for direct media URLs in SegmentTemplate elements
            var mediaTemplates = adaptationSet.Descendants(ns + "SegmentTemplate")
                .Select(st => st.Attribute("media")?.Value)
                .Where(url => !string.IsNullOrEmpty(url))
                .ToArray();
                
            if (mediaTemplates.Any())
                return mediaTemplates!;
        }
        
        var mediaTemplate = segmentTemplate?.Attribute("media")?.Value;
        if (string.IsNullOrEmpty(mediaTemplate))
            return Array.Empty<string>();
        
        // Extract timeline segments
        var timeline = adaptationSet.Descendants(ns + "SegmentTimeline").FirstOrDefault();
        if (timeline == null)
        {
            // Simple case: return the template URL (for testing)
            return new[] { mediaTemplate };
        }
        
        // Generate URLs from timeline
        var segments = timeline.Descendants(ns + "S");
        var urls = new List<string>();
        var segmentNumber = 1;
        
        foreach (var segment in segments)
        {
            var repeat = int.TryParse(segment.Attribute("r")?.Value, out var r) ? r + 1 : 1;
            
            for (int i = 0; i < repeat; i++)
            {
                var url = mediaTemplate
                    .Replace("$Number$", segmentNumber.ToString())
                    .Replace("$Number%06d$", segmentNumber.ToString("D6"))
                    .Replace("$RepresentationID$", "audio_flac_44100_1411");
                urls.Add(url);
                segmentNumber++;
            }
        }
        
        return urls.ToArray();
    }
    
    private string DetermineFileExtension(string codec, string sampleUrl)
    {
        // Check codec first
        if (codec.Contains("flac", StringComparison.OrdinalIgnoreCase))
            return ".flac";
        if (codec.Contains("mp4a", StringComparison.OrdinalIgnoreCase))
            return ".m4a";
        
        // Fallback: check URL
        if (sampleUrl.Contains(".flac", StringComparison.OrdinalIgnoreCase))
            return ".flac";
        if (sampleUrl.Contains(".mp4", StringComparison.OrdinalIgnoreCase))
            return codec.Contains("flac") ? ".flac" : ".m4a";
        if (sampleUrl.Contains(".ts", StringComparison.OrdinalIgnoreCase))
            return ".ts";
            
        // Default fallback
        return ".m4a";
    }
    
    private static string CreateTestDashManifest()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
        <MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" type=""static"" mediaPresentationDuration=""PT240S"">
            <Period start=""PT0S"">
                <AdaptationSet id=""0"" codecs=""flac"" mimeType=""audio/flac"" audioSamplingRate=""44100"">
                    <SegmentTemplate media=""https://audio-fa.scdn.co/$Number%06d$.flac"" startNumber=""1"" />
                    <SegmentTimeline>
                        <S d=""5000"" r=""1"" />
                        <S d=""5000"" r=""0"" />
                    </SegmentTimeline>
                </AdaptationSet>
            </Period>
        </MPD>";
    }
    
    private static string CreateTestBtsManifest()
    {
        return @"{
            ""urls"": [
                ""https://test.tidal.com/chunk1.flac"",
                ""https://test.tidal.com/chunk2.flac""
            ],
            ""codecs"": ""flac"",
            ""mimeType"": ""audio/flac"",
            ""encryptionType"": ""NONE""
        }";
    }
}
