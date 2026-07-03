using Tidalarr.Core.Models;

namespace Tidalarr.Core.Constants;

public static class TidalConstants
{
    // ─── Canonical plugin identity ───────────────────────────────────────────
    // Matches the apple + qobuz convention so cross-plugin parity tooling (the
    // ecosystem matrix in Lidarr.Plugin.Common/docs/ECOSYSTEM_PARITY_MATRIX.md)
    // can find a uniform PluginName/ServiceName/PluginVendor triple across all
    // streaming plugins. These are the user-facing brand strings; the host's
    // `NzbDrone.Core.Plugins.Plugin.Name` override in TidalarrInstalledPlugin.cs
    // is the source of truth for the System→Plugins UI listing.
    public const string PluginName = "Tidalarr";
    public const string ServiceName = "Tidal";
    public const string PluginVendor = "RicherTunes";

    // OAuth Client Credentials - MUST match TidalSharp
    public const string CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU";
    public const string CLIENT_SECRET_PKCE = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";

    public const string CLIENT_ID = "zU4XHVVkc2tDPo4t";
    public const string REDIRECT_URI = "https://tidal.com/android/login/auth";

    // API Endpoints
    public const string API_V1_BASE = "https://api.tidal.com/v1/";
    public const string AUTH_BASE = "https://auth.tidal.com/v1/oauth2/token";
    public const string LOGIN_BASE = "https://login.tidal.com/authorize";

    // OAuth Parameters
    // Space-delimited to match OAuth2 spec; FormUrlEncodedContent will encode spaces as '+'.
    // Include offline_access to obtain refresh tokens for long-lived sessions.
    public const string OAUTH_SCOPE = "r_usr w_usr w_sub offline_access";
    public const string APP_MODE = "android";
    public const string LANGUAGE = "EN";

    // Quality Mappings
    public static readonly Dictionary<TidalQuality, string> QualityParameters = new()
    {
        [TidalQuality.Low] = "LOW",
        [TidalQuality.High] = "HIGH",
        [TidalQuality.Lossless] = "LOSSLESS",
        [TidalQuality.HiRes] = "HI_RES_LOSSLESS"
    };

    // API Limits
    public const int DEFAULT_SEARCH_LIMIT = 100;
    public const int MAX_SEARCH_LIMIT = 1000;
    public const int DEFAULT_ITEM_LIMIT = 1000;

    // Favorites (import-list) pagination. Tidal's users/{id}/favorites/{albums,artists}
    // endpoints page with limit/offset; 50 is the server's conventional page size and keeps
    // each request cheap while a large library still pages through completely.
    public const int FAVORITES_PAGE_LIMIT = 50;

    // Defensive upper bound on favorites pages, guarding against a server that misreports
    // totalNumberOfItems (or never advances) so pagination can never loop unbounded. At 50
    // items/page this covers 500k favorited items — far beyond any real Tidal library.
    public const int FAVORITES_MAX_PAGES = 10_000;
}

