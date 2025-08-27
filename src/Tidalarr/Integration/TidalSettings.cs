using Lidarr.Plugin.Common.Base;

namespace Tidalarr.Integration;

public class TidalSettings : BaseStreamingSettings
{
    public string TidalMarket { get; set; } = "US";
    public bool IncludeMqa { get; set; } = true;
    public string RedirectUrl { get; set; } = string.Empty;
    public string PreferredQuality { get; set; } = "Lossless";
    public bool EnableCache { get; set; } = true;
    public int CacheDuration { get; set; } = 15;
    
    public override bool IsValid(out string errorMessage)
    {
        // Call base validation first
        if (!base.IsValid(out errorMessage))
            return false;
            
        // Validate Tidal-specific settings
        if (string.IsNullOrWhiteSpace(RedirectUrl))
        {
            errorMessage = "Redirect URL is required";
            return false;
        }
        
        if (!Uri.TryCreate(RedirectUrl, UriKind.Absolute, out var uri))
        {
            errorMessage = "Invalid redirect URL format";
            return false;
        }
        
        if (!uri.Host.Equals("tidal.com", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Invalid callback domain - must be tidal.com";
            return false;
        }
        
        if (!IsValidMarket(TidalMarket))
        {
            errorMessage = $"Invalid market '{TidalMarket}'. Supported: US, UK, DE, FR, CA, AU, JP";
            return false;
        }
        
        errorMessage = string.Empty;
        return true;
    }
    
    private static bool IsValidMarket(string market)
    {
        var validMarkets = new[] { "US", "UK", "DE", "FR", "CA", "AU", "JP" };
        return validMarkets.Contains(market, StringComparer.OrdinalIgnoreCase);
    }
}