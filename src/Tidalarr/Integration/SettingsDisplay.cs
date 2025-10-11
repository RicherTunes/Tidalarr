namespace Tidalarr.Integration;

public static class SettingsDisplay
{
    public static class Indexer
    {
        public const int ConfigPathOrder = 0;
        public const string ConfigPathLabel = "Config Path";

        public const int RedirectUrlOrder = 1;
        public const string RedirectUrlLabel = "Redirect URL";

        public const int MarketOrder = 2;
        public const string MarketLabel = "Market";

        public const int EarlyDownloadLimitOrder = 3;
        public const string EarlyDownloadLimitLabel = "Early Download Limit";
        public const string EarlyDownloadLimitUnit = "days";

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

        public const int DownloadPathOrder = 21;
        public const string DownloadPathLabel = "Download Path";

        public const int IncludeMqaOrder = 22;
        public const string IncludeMqaLabel = "Include MQA Masters";

        public const int ExtractFlacOrder = 23;
        public const string ExtractFlacLabel = "Extract FLAC from M4A";

        public const int ReEncodeAACOrder = 24;
        public const string ReEncodeAACLabel = "Re-encode AAC Streams";

        public const int SaveSyncedLyricsOrder = 25;
        public const string SaveSyncedLyricsLabel = "Save Synced Lyrics";

        public const int UseLrclibOrder = 26;
        public const string UseLrclibLabel = "Use LRCLIB for Lyrics";

        public const int ChunkDelayOrder = 27;
        public const string ChunkDelayLabel = "Chunk Delay";
        public const string ChunkDelayUnit = "ms";

        public const int ChunkDelayMinOrder = 28;
        public const string ChunkDelayMinLabel = "Min Chunk Delay";
        public const string ChunkDelayMinUnit = "ms";

        public const int ChunkDelayMaxOrder = 29;
        public const string ChunkDelayMaxLabel = "Max Chunk Delay";
        public const string ChunkDelayMaxUnit = "ms";
    }
}

