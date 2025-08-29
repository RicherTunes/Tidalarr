using Lidarr.Plugin.Common.Base;

namespace Tidalarr.Integration;

public class TidalIndexerSettings : BaseStreamingSettings
{
    public string TidalMarket { get; set; } = "US";
    public string RedirectUrl { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
    public int EarlyReleaseLimit { get; set; } = 14;
    public bool EnableCache { get; set; } = true;
    public new int CacheDuration { get; set; } = 15;
    
    // Required by Lidarr indexer interface (unused but mandatory)
    public override string BaseUrl { get; set; } = "https://api.tidal.com";
    
    public override bool IsValid(out string errorMessage)
    {
        // Call base validation first
        if (!base.IsValid(out errorMessage))
            return false;
            
        // Validate Tidal-specific settings
        if (string.IsNullOrWhiteSpace(RedirectUrl))
        {
            errorMessage = "Redirect URL is required for OAuth authentication";
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
        
        if (string.IsNullOrWhiteSpace(ConfigPath))
        {
            errorMessage = "Config path is required for storing authentication data";
            return false;
        }
        
        if (!IsValidMarket(TidalMarket))
        {
            errorMessage = $"Invalid market '{TidalMarket}'. Supported: US, UK, DE, FR, CA, AU, JP";
            return false;
        }
        
        if (EarlyReleaseLimit < 0 || EarlyReleaseLimit > 365)
        {
            errorMessage = "Early release limit must be between 0 and 365 days";
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