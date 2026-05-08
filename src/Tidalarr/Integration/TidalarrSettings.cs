using FluentValidation;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Base;
using Lidarr.Plugin.Common.Hosting;
using Tidalarr.Core.Models;
using FieldDefinition = Tidalarr.Integration.Annotations.FieldDefinitionAttribute;
using FieldType = Tidalarr.Integration.Annotations.FieldType;

namespace Tidalarr.Integration;

public class TidalarrSettings : BaseStreamingSettings
{
    private static readonly TidalarrSettingsValidator Validator = new();

    [FieldDefinition(SettingsDisplay.Indexer.ConfigPathOrder, Label = SettingsDisplay.Indexer.ConfigPathLabel, Type = FieldType.Textbox, HelpText = SettingsDisplay.Indexer.ConfigPathHelpText)]
    public string ConfigPath { get; set; } = PluginConfigRoots.Resolve("Tidalarr");

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

    [FieldDefinition(SettingsDisplay.Download.PreferredQualityOrder, Label = SettingsDisplay.Download.PreferredQualityLabel, Type = FieldType.Select, SelectOptions = typeof(TidalQuality), HelpText = SettingsDisplay.Download.PreferredQualityHelpText)]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(SettingsDisplay.Download.DownloadPathOrder, Label = SettingsDisplay.Download.DownloadPathLabel, Type = FieldType.Path, HelpText = SettingsDisplay.Download.DownloadPathHelpText)]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(SettingsDisplay.Download.IncludeMqaOrder, Label = SettingsDisplay.Download.IncludeMqaLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = SettingsDisplay.Download.IncludeMqaHelpText)]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.ExtractFlacOrder, Label = SettingsDisplay.Download.ExtractFlacLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = SettingsDisplay.Download.ExtractFlacHelpText)]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.ReEncodeAACOrder, Label = SettingsDisplay.Download.ReEncodeAACLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = SettingsDisplay.Download.ReEncodeAACHelpText)]
    public bool ReEncodeAAC { get; set; } = false;

    [FieldDefinition(SettingsDisplay.Download.SaveSyncedLyricsOrder, Label = SettingsDisplay.Download.SaveSyncedLyricsLabel, Type = FieldType.Checkbox, Advanced = true)]
    public bool SaveSyncedLyrics { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.UseLrclibOrder, Label = SettingsDisplay.Download.UseLrclibLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = SettingsDisplay.Download.UseLrclibHelpText)]
    public bool UseLRCLIB { get; set; } = false;

    [FieldDefinition(SettingsDisplay.Download.ChunkDelayOrder, Label = SettingsDisplay.Download.ChunkDelayLabel, Type = FieldType.Number, Unit = SettingsDisplay.Download.ChunkDelayUnit, Advanced = true, HelpText = SettingsDisplay.Download.ChunkDelayHelpText)]
    public int DownloadDelay { get; set; } = 0;

    [FieldDefinition(SettingsDisplay.Download.MaxConcurrentTrackDownloadsOrder, Label = SettingsDisplay.Download.MaxConcurrentTrackDownloadsLabel, Type = FieldType.Number, Advanced = true, HelpText = SettingsDisplay.Download.MaxConcurrentTrackDownloadsHelpText)]
    public int MaxConcurrentTrackDownloads { get; set; } = 2;

    [FieldDefinition(SettingsDisplay.Download.MaxConcurrentChunkDownloadsOrder, Label = SettingsDisplay.Download.MaxConcurrentChunkDownloadsLabel, Type = FieldType.Number, Advanced = true, HelpText = SettingsDisplay.Download.MaxConcurrentChunkDownloadsHelpText)]
    public int MaxConcurrentChunkDownloads { get; set; } = 2;

    public override string BaseUrl { get; set; } = "https://api.tidal.com";

    public override bool IsValid(out string errorMessage)
    {
        ValidationResult validation = Validator.Validate(this);
        errorMessage = validation.IsValid ? string.Empty : validation.ToString();
        return validation.IsValid;
    }

    public ValidationResult ValidateFluent()
    {
        return Validator.Validate(this);
    }

    /// <summary>
    /// Validate and return errors as simple types so callers (e.g., HostBridge) don't need a
    /// direct FluentValidation reference. Each error is (PropertyName, ErrorMessage).
    /// </summary>
    public (bool IsValid, List<(string Property, string Error)> Errors) ValidateSimple()
    {
        ValidationResult result = Validator.Validate(this);
        List<(string, string)> errors = result.Errors
            .Select(e => (e.PropertyName, e.ErrorMessage))
            .ToList();
        return (result.IsValid, errors);
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

            _ = RuleFor(x => x.MaxConcurrentTrackDownloads)
                .InclusiveBetween(1, 3)
                .WithMessage("Max concurrent track downloads must be between 1 and 3")
                .WithErrorCode(TidalarrValidationCodes.MaxConcurrentTrackDownloadsRange);

            _ = RuleFor(x => x.MaxConcurrentChunkDownloads)
                .InclusiveBetween(1, 8)
                .WithMessage("Max concurrent chunk downloads must be between 1 and 8")
                .WithErrorCode(TidalarrValidationCodes.MaxConcurrentChunkDownloadsRange);

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
