using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using NLog;

namespace Tidalarr.Domain.Streaming;

public enum ManifestMimeType
{
    MPD,    // DASH XML manifest
    BTS     // Binary Transport Stream
}

public class TidalStreamManifest
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // Shared, spec-correct MPEG-DASH segment-index parser (Common). Replaces tidalarr's former
    // in-house DASH walk so both manifest entry points route through one code path.
    private static readonly Lidarr.Plugin.Common.Services.Streaming.Manifests.DashManifestParser DashParser = new();

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
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to parse Tidal stream manifest; falling back to empty chunk list");
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

            // Navigate the DASH structure: MPD > Period > AdaptationSet > Representation.
            // Codec/extension are read from the first Representation to preserve historical
            // behavior (and the "no Representation => keep defaults" contract).
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

                // Segment index resolution (init segment at index 0, then @startNumber-based
                // $Number$ numbering with SegmentTimeline r=+1 repeat semantics) is delegated to
                // Common's shared, spec-correct parser. Tidal manifests carry absolute segment
                // URLs, so no base URL is required.
                Lidarr.Plugin.Common.Services.Streaming.Manifests.StreamManifest parsed =
                    DashParser.Parse(decodedManifest, string.Empty);
                ChunkUrls = [.. parsed.Segments.Select(s => s.Url)];
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to parse DASH manifest (base64 decode / XML parse / segment template resolution); falling back to empty chunk list");
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


