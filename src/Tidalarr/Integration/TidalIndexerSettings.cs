using FluentValidation;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Base;
using Tidalarr.Infrastructure.Storage;
using FieldDefinition = Tidalarr.Integration.Annotations.FieldDefinitionAttribute;
using FieldType = Tidalarr.Integration.Annotations.FieldType;

namespace Tidalarr.Integration;

public class TidalIndexerSettings : BaseStreamingSettings
{
    private static readonly TidalIndexerSettingsValidator Validator = new();

    [FieldDefinition(SettingsDisplay.Indexer.ConfigPathOrder, Label = SettingsDisplay.Indexer.ConfigPathLabel, Type = FieldType.Textbox, HelpText = SettingsDisplay.Indexer.ConfigPathHelpText)]
    public string ConfigPath { get; set; } = ConfigPathDefaults.GetDefaultConfigPath("Tidalarr");

    [FieldDefinition(SettingsDisplay.Indexer.RedirectUrlOrder, Label = SettingsDisplay.Indexer.RedirectUrlLabel, Type = FieldType.Textbox, HelpText = SettingsDisplay.Indexer.RedirectUrlHelpText)]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(SettingsDisplay.Indexer.MarketOrder, Label = SettingsDisplay.Indexer.MarketLabel, Type = FieldType.Textbox, HelpText = SettingsDisplay.Indexer.MarketHelpText, Advanced = true)]
    public string TidalMarket { get; set; } = "US";

    [FieldDefinition(SettingsDisplay.Indexer.EarlyDownloadLimitOrder, Label = SettingsDisplay.Indexer.EarlyDownloadLimitLabel, Type = FieldType.Number, Unit = SettingsDisplay.Indexer.EarlyDownloadLimitUnit, HelpText = SettingsDisplay.Indexer.EarlyDownloadLimitHelpText, Advanced = true)]
    public int? EarlyReleaseLimit { get; set; } = 14;

    [FieldDefinition(SettingsDisplay.Indexer.EnableCacheOrder, Label = SettingsDisplay.Indexer.EnableCacheLabel, Type = FieldType.Checkbox, Advanced = true)]
    public bool EnableCache { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Indexer.CacheDurationOrder, Label = SettingsDisplay.Indexer.CacheDurationLabel, Type = FieldType.Number, Unit = SettingsDisplay.Indexer.CacheDurationUnit, Advanced = true)]
    public new int CacheDuration { get; set; } = 15;

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

    private sealed class TidalIndexerSettingsValidator : AbstractValidator<TidalIndexerSettings>
    {
        public TidalIndexerSettingsValidator()
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
        }

        private static bool BeValidHttpUri(string redirect)
        {
            return Uri.TryCreate(redirect, UriKind.Absolute, out Uri? uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
