using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Tidalarr.Domain.Streaming;

public enum ManifestMimeType
{
    MPD,    // DASH XML manifest
    BTS     // Binary Transport Stream
}

public class TidalStreamManifest
{
    public string[] ChunkUrls { get; private set; } = [];
    public string FileExtension { get; private set; } = ".m4a";
    public string Codecs { get; private set; } = "MP4A";
    public string? KeyId { get; private set; } = string.Empty;
    public string? SecurityToken { get; private set; } = null;
    public bool IsEncrypted => !string.IsNullOrWhiteSpace(SecurityToken);

    public ManifestMimeType MimeType { get; private set; }

    public TidalStreamManifest(JsonElement streamData)
    {
        ParseStreamData(streamData);
    }

    private void ParseStreamData(JsonElement streamData)
    {
        try
        {
            // Extract manifest information
            string? manifestMimeType = streamData.GetProperty("manifestMimeType").GetString();
            string? encodedManifest = streamData.GetProperty("manifest").GetString();

            MimeType = manifestMimeType switch
            {
                "application/dash+xml" => ManifestMimeType.MPD,
                "application/vnd.tidal.bts" => ManifestMimeType.BTS,
                _ => ManifestMimeType.MPD
            };

            // Get encryption info if available
            if (streamData.TryGetProperty("keyId", out JsonElement keyIdElement))
            {
                KeyId = keyIdElement.GetString() ?? string.Empty;
            }
            if (streamData.TryGetProperty("securityToken", out JsonElement tokenElement))
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
        catch (Exception)
        {
            // Fallback to empty manifest
            ChunkUrls = [];
        }
    }

    private void ParseDashManifest(string encodedManifest)
    {
        try
        {
            // Base64 decode the manifest
            byte[] rawManifest = Convert.FromBase64String(encodedManifest);
            string decodedManifest = Encoding.UTF8.GetString(rawManifest);

            // Use XDocument for better LINQ support (following TidalSharp pattern)
            XDocument doc = XDocument.Parse(decodedManifest);
            XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            // Navigate the DASH structure: MPD > Period > AdaptationSet > Representation
            XElement? adaptationSet = doc.Root?
                .Elements(ns + "Period").FirstOrDefault()?
                .Elements(ns + "AdaptationSet").FirstOrDefault();

            XElement? representation = adaptationSet?
                .Elements(ns + "Representation").FirstOrDefault();

            if (representation != null)
            {
                // Get codec information from representation
                string codecsAttr = representation.Attribute("codecs")?.Value ?? "";
                Codecs = ParseCodecs(codecsAttr);
                FileExtension = DetermineFileExtension(codecsAttr);

                // Extract RepresentationID for template resolution
                string representationId = representation.Attribute("id")?.Value ?? "0";

                // Extract segment template
                XElement? segmentTemplate = representation.Elements(ns + "SegmentTemplate").FirstOrDefault();
                if (segmentTemplate != null)
                {
                    string mediaTemplate = segmentTemplate.Attribute("media")?.Value ?? "";
                    string initializationTemplate = segmentTemplate.Attribute("initialization")?.Value ?? "";

                    // Handle startNumber (TidalSharp pattern)
                    uint startNumber = uint.TryParse(segmentTemplate.Attribute("startNumber")?.Value, out uint start) ? start : 1;

                    if (!string.IsNullOrEmpty(mediaTemplate))
                    {
                        List<string> urls = [];

                        // Add initialization segment if present
                        if (!string.IsNullOrEmpty(initializationTemplate))
                        {
                            urls.Add(initializationTemplate
                                .Replace("$RepresentationID$", representationId)
                                .Replace("$Number$", "0"));
                        }

                        // Process segment timeline (corrected TidalSharp approach)
                        XElement? segmentTimeline = segmentTemplate.Elements(ns + "SegmentTimeline").FirstOrDefault();
                        if (segmentTimeline != null)
                        {
                            uint segmentNumber = startNumber; // Use startNumber as initial value

                            foreach (XElement s in segmentTimeline.Elements(ns + "S"))
                            {
                                // Critical fix: TidalSharp uses (1 + r) not (r + 1)
                                int repeat = int.TryParse(s.Attribute("r")?.Value, out int r) ? r : 0;
                                int segmentCount = 1 + repeat; // 1 occurrence + r repeats

                                // Generate segments with 0-based indexing (TidalSharp pattern)
                                for (int i = 0; i < segmentCount; i++)
                                {
                                    string url = mediaTemplate
                                        .Replace("$RepresentationID$", representationId)
                                        .Replace("$Number$", segmentNumber.ToString())
                                        .Replace("$Number%06d$", segmentNumber.ToString("D6")); // Support padded numbers
                                    urls.Add(url);
                                    segmentNumber++;
                                }
                            }
                        }

                        ChunkUrls = [.. urls];
                    }
                }
            }
        }
        catch (Exception)
        {
            ChunkUrls = [];
        }
    }

    private void ParseBtsManifest(string encodedManifest)
    {
        // BTS format is simpler - just the direct URL
        ChunkUrls = [encodedManifest];
        FileExtension = ".m4a";
        Codecs = "MP4A";
    }

    private string ParseCodecs(string codecsAttr)
    {
        return codecsAttr.Contains("flac") ? "FLAC" : codecsAttr.Contains("mp4a.40.5") || codecsAttr.Contains("mp4a") ? "MP4A" : "MP4A";
    }

    private string DetermineFileExtension(string codecsAttr)
    {
        // Tidal always delivers in M4A containers, regardless of codec inside
        // FLAC codec is inside M4A container
        if (codecsAttr.Contains("flac"))
        {
            return ".m4a"; // FLAC inside M4A - will extract later if needed
        }
        else
        {
            return ".m4a"; // AAC inside M4A
        }
    }
}


