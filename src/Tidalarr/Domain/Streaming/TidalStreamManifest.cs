using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Tidalarr.Domain.Streaming;

public enum ManifestMimeType
{
    MPD,    // DASH XML manifest
    BTS     // Binary Transport Stream
}

public class StreamManifest
{
    public string[] ChunkUrls { get; private set; } = Array.Empty<string>();
    public string FileExtension { get; private set; } = ".m4a";
    public string Codecs { get; private set; } = "MP4A";
    public string? KeyId { get; private set; } = string.Empty;
    public string? SecurityToken { get; private set; } = null;
    public bool IsEncrypted => !string.IsNullOrWhiteSpace(SecurityToken);

    public ManifestMimeType MimeType { get; private set; }

    public StreamManifest(JsonElement streamData)
    {
        ParseStreamData(streamData);
    }

    private void ParseStreamData(JsonElement streamData)
    {
        try
        {
            // Extract manifest information
            var manifestMimeType = streamData.GetProperty("manifestMimeType").GetString();
            var encodedManifest = streamData.GetProperty("manifest").GetString();
            
            MimeType = manifestMimeType switch
            {
                "application/dash+xml" => ManifestMimeType.MPD,
                "application/vnd.tidal.bts" => ManifestMimeType.BTS,
                _ => ManifestMimeType.MPD
            };

            // Get encryption info if available
            if (streamData.TryGetProperty("keyId", out var keyIdElement))
            {
                KeyId = keyIdElement.GetString() ?? string.Empty;
            }
            if (streamData.TryGetProperty("securityToken", out var tokenElement))
            {
                SecurityToken = tokenElement.GetString();
            }

            if (!string.IsNullOrEmpty(encodedManifest))
            {
                if (MimeType == ManifestMimeType.MPD)
                {
                    ParseDashManifest(encodedManifest);
                }
                else
                {
                    ParseBtsManifest(encodedManifest);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error parsing stream manifest: {ex.Message}");
            // Fallback to empty manifest
            ChunkUrls = Array.Empty<string>();
        }
    }

    private void ParseDashManifest(string encodedManifest)
    {
        try
        {
            // Base64 decode the manifest
            var rawManifest = Convert.FromBase64String(encodedManifest);
            var decodedManifest = Encoding.UTF8.GetString(rawManifest);
            
            // Use XDocument for better LINQ support (following TidalSharp pattern)
            var doc = XDocument.Parse(decodedManifest);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            
            // Navigate the DASH structure: MPD > Period > AdaptationSet > Representation
            var adaptationSet = doc.Root?
                .Elements(ns + "Period").FirstOrDefault()?
                .Elements(ns + "AdaptationSet").FirstOrDefault();
                
            var representation = adaptationSet?
                .Elements(ns + "Representation").FirstOrDefault();
            
            if (representation != null)
            {
                // Get codec information from representation
                var codecsAttr = representation.Attribute("codecs")?.Value ?? "";
                Codecs = ParseCodecs(codecsAttr);
                FileExtension = DetermineFileExtension(codecsAttr);
                
                // Extract RepresentationID for template resolution
                var representationId = representation.Attribute("id")?.Value ?? "0";
                
                // Extract segment template
                var segmentTemplate = representation.Elements(ns + "SegmentTemplate").FirstOrDefault();
                if (segmentTemplate != null)
                {
                    var mediaTemplate = segmentTemplate.Attribute("media")?.Value ?? "";
                    var initializationTemplate = segmentTemplate.Attribute("initialization")?.Value ?? "";
                    
                    // Handle startNumber (TidalSharp pattern)
                    var startNumber = uint.TryParse(segmentTemplate.Attribute("startNumber")?.Value, out var start) ? start : 1;
                    
                    if (!string.IsNullOrEmpty(mediaTemplate))
                    {
                        var urls = new List<string>();
                        
                        // Add initialization segment if present
                        if (!string.IsNullOrEmpty(initializationTemplate))
                        {
                            urls.Add(initializationTemplate
                                .Replace("$RepresentationID$", representationId)
                                .Replace("$Number$", "0"));
                        }
                        
                        // Process segment timeline (corrected TidalSharp approach)
                        var segmentTimeline = segmentTemplate.Elements(ns + "SegmentTimeline").FirstOrDefault();
                        if (segmentTimeline != null)
                        {
                            uint segmentNumber = startNumber; // Use startNumber as initial value
                            
                            foreach (var s in segmentTimeline.Elements(ns + "S"))
                            {
                                // Critical fix: TidalSharp uses (1 + r) not (r + 1)
                                var repeat = int.TryParse(s.Attribute("r")?.Value, out var r) ? r : 0;
                                var segmentCount = 1 + repeat; // 1 occurrence + r repeats
                                
                                // Generate segments with 0-based indexing (TidalSharp pattern)
                                for (int i = 0; i < segmentCount; i++)
                                {
                                    var url = mediaTemplate
                                        .Replace("$RepresentationID$", representationId)
                                        .Replace("$Number$", segmentNumber.ToString())
                                        .Replace("$Number%06d$", segmentNumber.ToString("D6")); // Support padded numbers
                                    urls.Add(url);
                                    segmentNumber++;
                                }
                            }
                        }
                        
                        ChunkUrls = urls.ToArray();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error parsing DASH manifest: {ex.Message}");
            Console.WriteLine($"⚠️ Manifest content preview: {encodedManifest?[..Math.Min(200, encodedManifest?.Length ?? 0)]}...");
            ChunkUrls = Array.Empty<string>();
        }
    }
    
    private void ParseBtsManifest(string encodedManifest)
    {
        // BTS format is simpler - just the direct URL
        ChunkUrls = new[] { encodedManifest };
        FileExtension = ".m4a";
        Codecs = "MP4A";
    }
    
    private string ParseCodecs(string codecsAttr)
    {
        if (codecsAttr.Contains("flac"))
            return "FLAC";
        else if (codecsAttr.Contains("mp4a.40.5") || codecsAttr.Contains("mp4a"))
            return "MP4A";
        else
            return "MP4A";
    }
    
    private string DetermineFileExtension(string codecsAttr)
    {
        // Tidal always delivers in M4A containers, regardless of codec inside
        // FLAC codec is inside M4A container
        if (codecsAttr.Contains("flac"))
            return ".m4a"; // FLAC inside M4A - will extract later if needed
        else
            return ".m4a"; // AAC inside M4A
    }
}




