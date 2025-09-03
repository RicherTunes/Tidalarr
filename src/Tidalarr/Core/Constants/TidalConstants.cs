using Tidalarr.Core.Models;

namespace Tidalarr.Core.Constants;

public static class TidalConstants
{
    // OAuth Client Credentials - MUST match TidalSharp
    public const string CLIENT_ID_PKCE = "6BDSRdpK9hqEBTgU";
    public const string CLIENT_SECRET_PKCE = "xeuPmY7nbpZ9IIbLAcQ93shka1VNheUAqN6IcszjTG8=";
    public const string REDIRECT_URI = "https://tidal.com/android/login/auth";

    // API Endpoints
    public const string API_V1_BASE = "https://api.tidal.com/v1/";
    public const string AUTH_BASE = "https://auth.tidal.com/v1/oauth2/token";
    public const string LOGIN_BASE = "https://login.tidal.com/authorize";

    // OAuth Parameters
    public const string OAUTH_SCOPE = "r_usr+w_usr+w_sub";
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
}

