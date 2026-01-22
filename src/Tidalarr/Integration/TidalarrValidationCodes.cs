namespace Tidalarr.Integration;

public static class TidalarrValidationCodes
{
    public const string ConfigPathRequired = "TID-CONFIG-REQUIRED";
    public const string ConfigPathInvalid = "TID-CONFIG-PATH";
    public const string RedirectRequired = "TID-REDIRECT-REQUIRED";
    public const string RedirectInvalidUri = "TID-REDIRECT-URI";
    public const string RedirectWrongDomain = "TID-REDIRECT-DOMAIN";
    public const string MarketUnsupported = "TID-MARKET-UNSUPPORTED";
    public const string EarlyReleaseRange = "TID-EARLY-OUTOFRANGE";
    public const string CacheDurationRange = "TID-CACHE-RANGE";
    public const string DownloadPathRequired = "TID-DOWNLOAD-REQUIRED";
    public const string DownloadPathInvalid = "TID-DOWNLOAD-PATH";
    public const string DownloadDelayRange = "TID-DOWNLOAD-DELAY";
    public const string PreferredQualityInvalid = "TID-QUALITY-INVALID";
}
