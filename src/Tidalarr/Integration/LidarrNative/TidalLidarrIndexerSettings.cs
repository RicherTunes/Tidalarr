using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Validation;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Lidarr-native indexer settings that implement IIndexerSettings for plugin discovery.
/// Provides UI fields visible in Lidarr's Settings > Indexers > Add > Tidalarr.
/// </summary>
public class TidalLidarrIndexerSettings : IIndexerSettings
{
    private static readonly TidalLidarrIndexerSettingsValidator Validator = new();

    public TidalLidarrIndexerSettings()
    {
        BaseUrl = "https://api.tidal.com";
        TidalMarket = "US";
        EarlyReleaseLimit = 14;
        EnableCache = true;
        CacheDuration = 15;
    }

    public string BaseUrl { get; set; }

    [FieldDefinition(0, Label = "OAuth Authorization URL", Type = FieldType.Textbox, Section = "Authentication",
        HelpText = "Convenience field (derived from Config Path). Click Test to generate an OAuth URL; you may need to refresh the settings page to see it. Changes to this field are ignored.")]
    public string OAuthAuthUrl
    {
        get => PKCEStateStore.TryReadAuthorizationUrl(ConfigPath) ?? string.Empty;
        set { }
    }

    [FieldDefinition(1, Label = "Config Path", Type = FieldType.Path, Section = "Authentication",
        HelpText = "Directory used to persist Tidal authentication tokens.")]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(2, Label = "OAuth Redirect URL", Type = FieldType.Textbox, Section = "Authentication",
        HelpText = "Click Test to generate an OAuth URL. Copy it from the error message, authenticate in browser, then paste the redirect URL here.")]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(3, Label = "Market", Type = FieldType.Textbox, Section = "Authentication", Advanced = true,
        HelpText = "Two-letter Tidal market code (US, UK, DE, FR, CA, AU, JP).")]
    public string TidalMarket { get; set; } = "US";

    [FieldDefinition(4, Label = "Early Release Limit", Type = FieldType.Number, Section = "Search", Advanced = true,
        HelpText = "Limit pre-release downloads to this many days before release. Range: 0-365, Default: 14")]
    public int? EarlyReleaseLimit { get; set; } = 14;

    [FieldDefinition(5, Label = "Enable Cache", Type = FieldType.Checkbox, Section = "Performance", Advanced = true,
        HelpText = "Cache search results to reduce API calls.")]
    public bool EnableCache { get; set; } = true;

    [FieldDefinition(6, Label = "Cache Duration (minutes)", Type = FieldType.Number, Section = "Performance", Advanced = true,
        HelpText = "How long to cache search results. Range: 0-1440, Default: 15")]
    public int CacheDuration { get; set; } = 15;

    public NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult(Validator.Validate(this));
    }

    /// <summary>
    /// Convert to the existing TidalIndexerSettings for business logic reuse.
    /// </summary>
    public TidalIndexerSettings ToTidalSettings()
    {
        return new TidalIndexerSettings
        {
            ConfigPath = ConfigPath,
            RedirectUrl = RedirectUrl,
            TidalMarket = TidalMarket,
            EarlyReleaseLimit = EarlyReleaseLimit,
            EnableCache = EnableCache,
            CacheDuration = CacheDuration
        };
    }
}

public class TidalLidarrIndexerSettingsValidator : AbstractValidator<TidalLidarrIndexerSettings>
{
    private static readonly string[] SupportedMarkets = ["US", "UK", "DE", "FR", "CA", "AU", "JP"];

    public TidalLidarrIndexerSettingsValidator()
    {
        _ = RuleFor(x => x.ConfigPath)
            .NotEmpty().WithMessage("Config path is required");

        // RedirectUrl validation: only validate format when provided (not required during initial setup)
        _ = RuleFor(x => x.RedirectUrl)
            .Must(BeValidHttpUri).WithMessage("Redirect URL must be a valid HTTP/HTTPS URL")
            .When(x => !string.IsNullOrWhiteSpace(x.RedirectUrl));

        _ = RuleFor(x => x.TidalMarket)
            .Must(market => SupportedMarkets.Contains(market, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Unsupported market. Supported values: US, UK, DE, FR, CA, AU, JP");

        _ = RuleFor(x => x.EarlyReleaseLimit)
            .InclusiveBetween(0, 365)
            .WithMessage("Early release limit must be between 0 and 365 days")
            .When(x => x.EarlyReleaseLimit.HasValue);

        _ = RuleFor(x => x.CacheDuration)
            .InclusiveBetween(0, 1440)
            .WithMessage("Cache duration must be between 0 and 1440 minutes");
    }

    private static bool BeValidHttpUri(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
