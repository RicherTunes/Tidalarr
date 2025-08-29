using Lidarr.Plugin.Common.Base;

namespace Tidalarr.Integration;

public class TidalDownloadSettings : BaseStreamingSettings
{
    public string PreferredQuality { get; set; } = "Lossless";
    public bool IncludeMqa { get; set; } = true;
    public string DownloadPath { get; set; } = string.Empty;
    public bool ExtractFlac { get; set; } = false;
    public bool ReEncodeAAC { get; set; } = false;
    public bool SaveSyncedLyrics { get; set; } = true;
    public bool UseLRCLIB { get; set; } = false;
    public int DownloadDelay { get; set; } = 1000;
    public int DownloadDelayMin { get; set; } = 500;
    public int DownloadDelayMax { get; set; } = 2000;
    
    public override bool IsValid(out string errorMessage)
    {
        // Call base validation first
        if (!base.IsValid(out errorMessage))
            return false;
            
        // Validate Tidal download-specific settings
        if (string.IsNullOrWhiteSpace(DownloadPath))
        {
            errorMessage = "Download path is required";
            return false;
        }
        
        if (!IsValidQuality(PreferredQuality))
        {
            errorMessage = $"Invalid quality '{PreferredQuality}'. Supported: Low, High, Lossless, HiRes";
            return false;
        }
        
        if (DownloadDelay < 0 || DownloadDelay > 60000)
        {
            errorMessage = "Download delay must be between 0 and 60000 milliseconds";
            return false;
        }
        
        if (DownloadDelayMin < 0 || DownloadDelayMin > DownloadDelayMax)
        {
            errorMessage = "Download delay minimum must be less than or equal to maximum";
            return false;
        }
        
        if (DownloadDelayMax < DownloadDelayMin || DownloadDelayMax > 60000)
        {
            errorMessage = "Download delay maximum must be greater than minimum and less than 60000ms";
            return false;
        }
        
        errorMessage = string.Empty;
        return true;
    }
    
    private static bool IsValidQuality(string quality)
    {
        var validQualities = new[] { "Low", "High", "Lossless", "HiRes" };
        return validQualities.Contains(quality, StringComparer.OrdinalIgnoreCase);
    }
}