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

    public TidalLidarrDownloadClientSettings()
    {
        PreferredQuality = TidalQuality.Lossless;
        IncludeMqa = true;
        ExtractFlac = true;
        DownloadDelay = 1000;
    }

    [FieldDefinition(0, Label = "OAuth Authorization URL", Type = FieldType.Textbox, Section = "Authentication",
        HelpText = "Convenience field (derived from Config Path). Click Test on the indexer to generate an OAuth URL; you may need to refresh the settings page to see it. Changes to this field are ignored.")]
    public string OAuthAuthUrl
    {
        get => PKCEStateStore.TryReadAuthorizationUrl(ConfigPath) ?? string.Empty;
        set { }
    }

    [FieldDefinition(1, Label = "Config Path", Type = FieldType.Path, Section = "Authentication",
        HelpText = "Directory used to persist Tidal authentication tokens. Must match the indexer config path to share authentication.")]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(2, Label = "OAuth Redirect URL", Type = FieldType.Textbox, Section = "Authentication",
        HelpText = "Same as indexer. If using shared ConfigPath, authentication is automatic after indexer setup.")]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(3, Label = "Download Path", Type = FieldType.Path, Section = "Download",
        HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(4, Label = "Preferred Quality", Type = FieldType.Select, SelectOptions = typeof(TidalQuality), Section = "Quality",
        HelpText = "Audio quality requested from Tidal.")]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(5, Label = "Include MQA", Type = FieldType.Checkbox, Section = "Quality", Advanced = true,
        HelpText = "Allow Master (MQA) releases when available.")]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(6, Label = "Extract FLAC", Type = FieldType.Checkbox, Section = "Quality", Advanced = true,
        HelpText = "Convert M4A containers to FLAC when possible.")]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(7, Label = "Chunk Delay (ms)", Type = FieldType.Number, Section = "Performance", Advanced = true,
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

        // RedirectUrl validation: only validate format when provided (not required during initial setup)
        _ = RuleFor(x => x.RedirectUrl)
            .Must(BeValidHttpUri).WithMessage("Redirect URL must be a valid HTTP/HTTPS URL")
            .When(x => !string.IsNullOrWhiteSpace(x.RedirectUrl));

        _ = RuleFor(x => x.DownloadPath)
            .NotEmpty().WithMessage("Download path is required");

        _ = RuleFor(x => x.PreferredQuality)
            .Must(quality => Enum.IsDefined(typeof(TidalQuality), quality))
            .WithMessage("Preferred quality selection is invalid");

        _ = RuleFor(x => x.DownloadDelay)
            .InclusiveBetween(0, 60000)
            .WithMessage("Chunk delay must be between 0 and 60000 milliseconds");
    }

    private static bool BeValidHttpUri(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
