using FluentValidation;
using Lidarr.Plugin.Common.Hosting;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Lidarr-native download client settings that implement IProviderConfig for plugin discovery.
/// Provides UI fields visible in Lidarr's Settings > Download Clients > Add > Tidalarr.
/// </summary>
public class TidalLidarrDownloadClientSettings : IProviderConfig
{
    private static readonly TidalLidarrDownloadClientSettingsValidator Validator = new();
    private static readonly string DefaultConfigPath = PluginConfigRoots.Resolve("Tidalarr");

    public TidalLidarrDownloadClientSettings()
    {
        ConfigPath = DefaultConfigPath;
        PreferredQuality = TidalQuality.Lossless;
        IncludeMqa = true;
        ExtractFlac = true;
        DownloadDelay = 0;
    }

    [FieldDefinition(0, Label = "Config Path", Type = FieldType.Path, Section = "Authentication",
        HelpText = "Directory containing Tidal authentication tokens. Must match the indexer's Config Path - authenticate via the indexer, then the download client uses the same tokens automatically.")]
    public string ConfigPath { get; set; } = DefaultConfigPath;

    [FieldDefinition(1, Label = "Download Path", Type = FieldType.Path, Section = "Download",
        HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(2, Label = "Preferred Quality", Type = FieldType.Select, SelectOptions = typeof(TidalQuality), Section = "Quality",
        HelpText = "Audio quality requested from Tidal. Low/High = AAC; Lossless = FLAC 16-bit/44.1kHz; HiRes = FLAC up to 24-bit/192kHz. Your Tidal subscription tier determines which qualities you can actually download — the plugin falls back to the highest available. HiFi Plus is required for Lossless+; the older HiFi tier maxes at AAC 320 (High).")]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(3, Label = "Include MQA", Type = FieldType.Checkbox, Section = "Quality", Advanced = true,
        HelpText = "Allow Master (MQA) releases when available.")]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(4, Label = "Extract FLAC", Type = FieldType.Checkbox, Section = "Quality", Advanced = true,
        HelpText = "Convert M4A containers to FLAC when possible.")]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(5, Label = "Chunk Delay (ms)", Type = FieldType.Number, Section = "Performance", Advanced = true,
        HelpText = "Delay between chunk requests in milliseconds. Use 0 for maximum speed, increase if rate-limited.")]
    public int DownloadDelay { get; set; } = 0;

    [FieldDefinition(6, Label = "Max Concurrent Track Downloads", Type = FieldType.Number, Section = "Performance", Advanced = true,
        HelpText = "Maximum number of tracks to download concurrently. Increase cautiously: higher values may increase memory usage and can trigger rate limiting.")]
    public int MaxConcurrentTrackDownloads { get; set; } = 2;

    [FieldDefinition(7, Label = "Max Concurrent Chunk Downloads", Type = FieldType.Number, Section = "Performance", Advanced = true,
        HelpText = "Maximum number of chunk requests to perform concurrently per track. Higher values can improve speed but may trigger rate limiting.")]
    public int MaxConcurrentChunkDownloads { get; set; } = 2;

    [FieldDefinition(8, Label = "Save Synced Lyrics", Type = FieldType.Checkbox, Section = "Metadata", Advanced = true,
        HelpText = "Save a synced .lrc lyrics file alongside each downloaded track when lyrics are available.")]
    public bool SaveSyncedLyrics { get; set; } = true;

    [FieldDefinition(9, Label = "Use LRCLIB for Lyrics", Type = FieldType.Checkbox, Section = "Metadata", Advanced = true,
        HelpText = "Fall back to the public LRCLIB service when Tidal does not provide synced lyrics. Sends artist/track/album names to lrclib.net.")]
    public bool UseLRCLIB { get; set; }

    public NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult(Validator.Validate(this));
    }

    /// <summary>
    /// Convert to the existing TidalDownloadClientSettings for business logic reuse.
    /// </summary>
    public TidalDownloadClientSettings ToTidalSettings()
    {
        return new TidalDownloadClientSettings
        {
            PreferredQuality = PreferredQuality,
            DownloadPath = DownloadPath,
            IncludeMqa = IncludeMqa,
            ExtractFlac = ExtractFlac,
            DownloadDelay = DownloadDelay,
            MaxConcurrentTrackDownloads = MaxConcurrentTrackDownloads,
            MaxConcurrentChunkDownloads = MaxConcurrentChunkDownloads,
            SaveSyncedLyrics = SaveSyncedLyrics,
            UseLRCLIB = UseLRCLIB
        };
    }
}

public class TidalLidarrDownloadClientSettingsValidator : AbstractValidator<TidalLidarrDownloadClientSettings>
{
    public TidalLidarrDownloadClientSettingsValidator()
    {
        _ = RuleFor(x => x.ConfigPath)
            .NotEmpty().WithMessage("Config path is required");

        _ = RuleFor(x => x.DownloadPath)
            .NotEmpty().WithMessage("Download path is required");

        _ = RuleFor(x => x.PreferredQuality)
            .Must(quality => Enum.IsDefined(typeof(TidalQuality), quality))
            .WithMessage("Preferred quality selection is invalid");

        _ = RuleFor(x => x.DownloadDelay)
            .InclusiveBetween(0, 60000)
            .WithMessage("Chunk delay must be between 0 and 60000 milliseconds");

        _ = RuleFor(x => x.MaxConcurrentTrackDownloads)
            .InclusiveBetween(1, 3)
            .WithMessage("Max concurrent track downloads must be between 1 and 3");

        _ = RuleFor(x => x.MaxConcurrentChunkDownloads)
            .InclusiveBetween(1, 8)
            .WithMessage("Max concurrent chunk downloads must be between 1 and 8");
    }
}
