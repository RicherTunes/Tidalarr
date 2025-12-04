using FluentValidation;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Base;
using Tidalarr.Core.Models;
using FieldDefinition = Tidalarr.Integration.Annotations.FieldDefinitionAttribute;
using FieldType = Tidalarr.Integration.Annotations.FieldType;

namespace Tidalarr.Integration;

public class TidalarrSettings : BaseStreamingSettings
{
    private static readonly TidalarrSettingsValidator Validator = new();

    [FieldDefinition(0, Label = "Config Path", Type = FieldType.Textbox, HelpText = "Directory used to persist Tidal authentication tokens.")]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(1, Label = "Redirect URL", Type = FieldType.Textbox, HelpText = "OAuth redirect URL captured after completing the Tidal login flow.")]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(2, Label = "Market", Type = FieldType.Textbox, HelpText = "Two-letter Tidal market code (US, UK, DE, FR, CA, AU, JP).", Advanced = true)]
    public string TidalMarket { get; set; } = "US";

    [FieldDefinition(3, Label = "Early Download Limit", Type = FieldType.Number, Unit = "days", HelpText = "Limit pre-release downloads to this many days before release.", Advanced = true)]
    public int? EarlyReleaseLimit { get; set; } = 14;

    [FieldDefinition(4, Label = "Enable Cache", Type = FieldType.Checkbox, Advanced = true)]
    public bool EnableCache { get; set; } = true;

    [FieldDefinition(5, Label = "Cache Duration", Type = FieldType.Number, Unit = "minutes", Advanced = true)]
    public new int CacheDuration { get; set; } = 15;

    [FieldDefinition(20, Label = "Preferred Quality", Type = FieldType.Select, SelectOptions = typeof(TidalQuality), HelpText = "Audio quality requested from Tidal.")]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(21, Label = "Download Path", Type = FieldType.Path, HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(22, Label = "Include MQA Masters", Type = FieldType.Checkbox, Advanced = true, HelpText = "Allow Master (MQA) releases when available.")]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(23, Label = "Extract FLAC from M4A", Type = FieldType.Checkbox, Advanced = true, HelpText = "Convert M4A containers to FLAC when possible.")]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(24, Label = "Re-encode AAC Streams", Type = FieldType.Checkbox, Advanced = true, HelpText = "Transcode AAC streams to 320kbps AAC when HiRes/Lossless are unavailable.")]
    public bool ReEncodeAAC { get; set; } = false;

    [FieldDefinition(25, Label = "Save Synced Lyrics", Type = FieldType.Checkbox, Advanced = true)]
    public bool SaveSyncedLyrics { get; set; } = true;

    [FieldDefinition(26, Label = "Use LRCLIB for Lyrics", Type = FieldType.Checkbox, Advanced = true, HelpText = "Fallback to LRCLIB when Tidal does not provide synced lyrics.")]
    public bool UseLRCLIB { get; set; } = false;

    [FieldDefinition(27, Label = "Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true, HelpText = "Delay between chunk requests used for throttling.")]
    public int DownloadDelay { get; set; } = 1000;

    [FieldDefinition(28, Label = "Min Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
    public int DownloadDelayMin { get; set; } = 500;

    [FieldDefinition(29, Label = "Max Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
    public int DownloadDelayMax { get; set; } = 2000;

    public override string BaseUrl { get; set; } = "https://api.tidal.com";

    public override bool IsValid(out string errorMessage)
    {
        ValidationResult validation = Validator.Validate(this);
        errorMessage = validation.IsValid ? string.Empty : validation.Errors.First().ErrorMessage;
        return validation.IsValid;
    }

    public ValidationResult ValidateFluent()
    {
        return Validator.Validate(this);
    }

    private static bool IsSupportedMarket(string? market)
    {
        return !string.IsNullOrWhiteSpace(market) && SupportedMarkets.Contains(market, StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string[] SupportedMarkets = ["US", "UK", "DE", "FR", "CA", "AU", "JP"];

    private sealed class TidalarrSettingsValidator : AbstractValidator<TidalarrSettings>
    {
        public TidalarrSettingsValidator()
        {
            _ = RuleFor(x => x.ConfigPath)
                .NotEmpty().WithMessage("Config path is required").WithErrorCode(TidalarrValidationCodes.ConfigPathRequired)
                .Must(PathValidationExtensions.IsReasonablePath).WithMessage("Config path is invalid").WithErrorCode(TidalarrValidationCodes.ConfigPathInvalid);

            _ = RuleFor(x => x.RedirectUrl)
                .NotEmpty().WithMessage("Redirect URL is required for OAuth authentication").WithErrorCode(TidalarrValidationCodes.RedirectRequired)
                .Must(BeValidHttpUri).WithMessage("Redirect URL must be an absolute HTTP/HTTPS URL").WithErrorCode(TidalarrValidationCodes.RedirectInvalidUri)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) && parsed.Host.EndsWith("tidal.com", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Redirect URL must be under the tidal.com domain").WithErrorCode(TidalarrValidationCodes.RedirectWrongDomain);

            _ = RuleFor(x => x.TidalMarket)
                .Must(IsSupportedMarket)
                .WithMessage("Unsupported market '{PropertyValue}'. Supported values: US, UK, DE, FR, CA, AU, JP")
                .WithErrorCode(TidalarrValidationCodes.MarketUnsupported);

            _ = RuleFor(x => x.EarlyReleaseLimit)
                .InclusiveBetween(0, 365)
                .WithMessage("Early release limit must be between 0 and 365 days")
                .WithErrorCode(TidalarrValidationCodes.EarlyReleaseRange)
                .When(x => x.EarlyReleaseLimit.HasValue);

            _ = RuleFor(x => x.CacheDuration)
                .InclusiveBetween(0, 1440)
                .WithMessage("Cache duration must be between 0 and 1440 minutes")
                .WithErrorCode(TidalarrValidationCodes.CacheDurationRange);

            _ = RuleFor(x => x.DownloadPath)
                .NotEmpty().WithMessage("Download path is required").WithErrorCode(TidalarrValidationCodes.DownloadPathRequired)
                .Must(PathValidationExtensions.IsReasonablePath).WithMessage("Download path is invalid").WithErrorCode(TidalarrValidationCodes.DownloadPathInvalid);

            _ = RuleFor(x => x.DownloadDelay)
                .InclusiveBetween(0, 60000)
                .WithMessage("Chunk delay must be between 0 and 60000 milliseconds")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayRange);

            _ = RuleFor(x => x.DownloadDelayMin)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum delay must be greater than or equal to 0 milliseconds")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayMinRange)
                .LessThanOrEqualTo(x => x.DownloadDelayMax)
                .WithMessage("Minimum delay must be less than or equal to maximum delay")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayMinRange);

            _ = RuleFor(x => x.DownloadDelayMax)
                .GreaterThanOrEqualTo(x => x.DownloadDelayMin)
                .WithMessage("Maximum delay must be greater than or equal to minimum delay")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayMaxRange)
                .LessThanOrEqualTo(60000)
                .WithMessage("Maximum delay must be between min delay and 60000 milliseconds")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayMaxRange);

            _ = RuleFor(x => x.PreferredQuality)
                .Must(quality => Enum.IsDefined(typeof(TidalQuality), quality))
                .WithMessage("Preferred quality selection is invalid")
                .WithErrorCode(TidalarrValidationCodes.PreferredQualityInvalid);
        }

        private static bool BeValidHttpUri(string redirect)
        {
            return Uri.TryCreate(redirect, UriKind.Absolute, out Uri? uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
