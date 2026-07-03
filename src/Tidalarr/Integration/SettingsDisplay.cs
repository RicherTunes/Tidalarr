namespace Tidalarr.Integration;

public static class SettingsDisplay
{
    public static class Indexer
    {
        public const int ConfigPathOrder = 0;
        public const string ConfigPathLabel = "Config Path";
        public const string ConfigPathHelpText = "Directory used to persist Tidal authentication tokens.";

        public const int RedirectUrlOrder = 1;
        public const string RedirectUrlLabel = "Redirect URL";
        public const string RedirectUrlHelpText = "OAuth redirect URL captured after completing the Tidal login flow.";

        public const int MarketOrder = 2;
        public const string MarketLabel = "Market";
        public const string MarketHelpText = "Two-letter Tidal market code (US, UK, DE, FR, CA, AU, JP).";

        public const int EarlyDownloadLimitOrder = 3;
        public const string EarlyDownloadLimitLabel = "Early Download Limit";
        public const string EarlyDownloadLimitUnit = "days";
        public const string EarlyDownloadLimitHelpText = "Limit pre-release downloads to this many days before release.";

        public const int EnableCacheOrder = 4;
        public const string EnableCacheLabel = "Enable Cache";

        public const int CacheDurationOrder = 5;
        public const string CacheDurationLabel = "Cache Duration";
        public const string CacheDurationUnit = "minutes";
    }

    public static class Download
    {
        public const int PreferredQualityOrder = 20;
        public const string PreferredQualityLabel = "Preferred Quality";
        public const string PreferredQualityHelpText = "Audio quality requested from Tidal.";

        public const int DownloadPathOrder = 21;
        public const string DownloadPathLabel = "Download Path";
        public const string DownloadPathHelpText = "Destination folder for downloaded albums.";

        public const int ExtractFlacOrder = 23;
        public const string ExtractFlacLabel = "Extract FLAC from M4A";
        public const string ExtractFlacHelpText = "Convert M4A containers to FLAC when possible.";

        public const int SaveSyncedLyricsOrder = 25;
        public const string SaveSyncedLyricsLabel = "Save Synced Lyrics";

        public const int UseLrclibOrder = 26;
        public const string UseLrclibLabel = "Use LRCLIB for Lyrics";
        public const string UseLrclibHelpText = "Fallback to LRCLIB when Tidal does not provide synced lyrics.";

        public const int ChunkDelayOrder = 27;
        public const string ChunkDelayLabel = "Chunk Delay";
        public const string ChunkDelayUnit = "ms";
        public const string ChunkDelayHelpText = "Delay between chunk requests in milliseconds. Use 0 for maximum speed, increase if rate-limited.";

        public const int MaxConcurrentTrackDownloadsOrder = 28;
        public const string MaxConcurrentTrackDownloadsLabel = "Max Concurrent Track Downloads";
        public const string MaxConcurrentTrackDownloadsHelpText = "Maximum number of tracks to download concurrently. Increase cautiously: higher values may increase memory usage and can trigger rate limiting.";

        public const int MaxConcurrentChunkDownloadsOrder = 29;
        public const string MaxConcurrentChunkDownloadsLabel = "Max Concurrent Chunk Downloads";
        public const string MaxConcurrentChunkDownloadsHelpText = "Maximum number of chunk requests to perform concurrently per track. Higher values can improve speed but may trigger rate limiting.";
    }
}
