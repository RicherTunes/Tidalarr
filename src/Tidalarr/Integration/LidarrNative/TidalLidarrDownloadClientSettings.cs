using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Lidarr-native download client settings that implement IProviderConfig for plugin discovery.
/// Provides UI fields visible in Lidarr's Settings > Download Clients > Add > Tidalarr.
/// </summary>
public class TidalLidarrDownloadClientSettings : IProviderConfig
{
    private static readonly TidalLidarrDownloadClientSettingsValidator Validator = new();
    private static readonly string DefaultConfigPath = ConfigPathDefaults.GetDefaultConfigPath("Tidalarr");

    public TidalLidarrDownloadClientSettings()
    {
        ConfigPath = DefaultConfigPath;
        PreferredQuality = TidalQuality.Lossless;
        IncludeMqa = true;
        ExtractFlac = true;
        DownloadDelay = 1000;
    }

    [FieldDefinition(0, Label = "Config Path", Type = FieldType.Path, Section = "Authentication",
        HelpText = "Directory containing Tidal authentication tokens. Must match the indexer's Config Path - authenticate via the indexer, then the download client uses the same tokens automatically.")]
    public string ConfigPath { get; set; } = DefaultConfigPath;

    [FieldDefinition(1, Label = "Download Path", Type = FieldType.Path, Section = "Download",
        HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(2, Label = "Preferred Quality", Type = FieldType.Select, SelectOptions = typeof(TidalQuality), Section = "Quality",
        HelpText = "Audio quality requested from Tidal.")]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(3, Label = "Include MQA", Type = FieldType.Checkbox, Section = "Quality", Advanced = true,
        HelpText = "Allow Master (MQA) releases when available.")]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(4, Label = "Extract FLAC", Type = FieldType.Checkbox, Section = "Quality", Advanced = true,
        HelpText = "Convert M4A containers to FLAC when possible.")]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(5, Label = "Chunk Delay (ms)", Type = FieldType.Number, Section = "Performance", Advanced = true,
        HelpText = "Delay between chunk requests used for throttling. Range: 0-60000, Default: 1000")]
    public int DownloadDelay { get; set; } = 1000;

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
            DownloadDelay = DownloadDelay
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
    }
}
