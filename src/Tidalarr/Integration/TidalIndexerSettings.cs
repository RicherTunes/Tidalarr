using System;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Base;
using FieldDefinition = Tidalarr.Integration.Annotations.FieldDefinitionAttribute;
using FieldType = Tidalarr.Integration.Annotations.FieldType;

namespace Tidalarr.Integration;

public class TidalIndexerSettings : BaseStreamingSettings
{
    private static readonly TidalIndexerSettingsValidator Validator = new();

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

    public override string BaseUrl { get; set; } = "https://api.tidal.com";

    public override bool IsValid(out string errorMessage)
    {
        var validation = Validator.Validate(this);
        errorMessage = validation.IsValid ? string.Empty : validation.Errors.First().ErrorMessage;
        return validation.IsValid;
    }

    public ValidationResult ValidateFluent()
    {
        return Validator.Validate(this);
    }

    private static bool IsSupportedMarket(string? market)
    {
        if (string.IsNullOrWhiteSpace(market))
        {
            return false;
        }

        return SupportedMarkets.Contains(market, StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string[] SupportedMarkets = { "US", "UK", "DE", "FR", "CA", "AU", "JP" };

    private sealed class TidalIndexerSettingsValidator : AbstractValidator<TidalIndexerSettings>
    {
        public TidalIndexerSettingsValidator()
        {
            RuleFor(x => x.ConfigPath)
                .NotEmpty().WithMessage("Config path is required").WithErrorCode(TidalarrValidationCodes.ConfigPathRequired)
                .Must(PathValidationExtensions.IsReasonablePath).WithMessage("Config path is invalid").WithErrorCode(TidalarrValidationCodes.ConfigPathInvalid);

            RuleFor(x => x.RedirectUrl)
                .NotEmpty().WithMessage("Redirect URL is required for OAuth authentication").WithErrorCode(TidalarrValidationCodes.RedirectRequired)
                .Must(BeValidHttpUri).WithMessage("Redirect URL must be an absolute HTTP/HTTPS URL").WithErrorCode(TidalarrValidationCodes.RedirectInvalidUri)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Host.EndsWith("tidal.com", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Redirect URL must be under the tidal.com domain").WithErrorCode(TidalarrValidationCodes.RedirectWrongDomain);

            RuleFor(x => x.TidalMarket)
                .Must(IsSupportedMarket)
                .WithMessage("Unsupported market '{PropertyValue}'. Supported values: US, UK, DE, FR, CA, AU, JP")
                .WithErrorCode(TidalarrValidationCodes.MarketUnsupported);

            RuleFor(x => x.EarlyReleaseLimit)
                .InclusiveBetween(0, 365)
                .WithMessage("Early release limit must be between 0 and 365 days")
                .WithErrorCode(TidalarrValidationCodes.EarlyReleaseRange)
                .When(x => x.EarlyReleaseLimit.HasValue);

            RuleFor(x => x.CacheDuration)
                .InclusiveBetween(0, 1440)
                .WithMessage("Cache duration must be between 0 and 1440 minutes")
                .WithErrorCode(TidalarrValidationCodes.CacheDurationRange);
        }

        private static bool BeValidHttpUri(string redirect)
        {
            return Uri.TryCreate(redirect, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
