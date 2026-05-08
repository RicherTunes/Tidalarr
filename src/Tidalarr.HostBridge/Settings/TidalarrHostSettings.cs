using NzbDrone.Core.Annotations;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.ThingiProvider;

namespace Tidalarr.HostBridge.Settings;

// Host-only wrapper that carries NzbDrone UI annotations and maps to core settings
public class TidalarrHostSettings : IIndexerSettings, IProviderConfig
{
    [FieldDefinition(Integration.SettingsDisplay.Indexer.ConfigPathOrder, Label = Integration.SettingsDisplay.Indexer.ConfigPathLabel, Type = FieldType.Textbox, HelpText = Integration.SettingsDisplay.Indexer.ConfigPathHelpText)]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.RedirectUrlOrder, Label = Integration.SettingsDisplay.Indexer.RedirectUrlLabel, Type = FieldType.Textbox, HelpText = Integration.SettingsDisplay.Indexer.RedirectUrlHelpText)]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.MarketOrder, Label = Integration.SettingsDisplay.Indexer.MarketLabel, Type = FieldType.Textbox, HelpText = Integration.SettingsDisplay.Indexer.MarketHelpText, Advanced = true)]
    public string TidalMarket { get; set; } = "US";

    [FieldDefinition(Integration.SettingsDisplay.Indexer.EarlyDownloadLimitOrder, Label = Integration.SettingsDisplay.Indexer.EarlyDownloadLimitLabel, Type = FieldType.Number, Unit = Integration.SettingsDisplay.Indexer.EarlyDownloadLimitUnit, HelpText = Integration.SettingsDisplay.Indexer.EarlyDownloadLimitHelpText, Advanced = true)]
    public int? EarlyReleaseLimit { get; set; } = 14;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.EnableCacheOrder, Label = Integration.SettingsDisplay.Indexer.EnableCacheLabel, Type = FieldType.Checkbox, Advanced = true)]
    public bool EnableCache { get; set; } = true;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.CacheDurationOrder, Label = Integration.SettingsDisplay.Indexer.CacheDurationLabel, Type = FieldType.Number, Unit = Integration.SettingsDisplay.Indexer.CacheDurationUnit, Advanced = true)]
    public int CacheDuration { get; set; } = 15;

    public string BaseUrl { get; set; } = "https://api.tidal.com";

    public NzbDrone.Core.Validation.NzbDroneValidationResult Validate()
    {
        // Delegate to core validator via simple types, then construct
        // NzbDroneValidationResult using the host's FluentValidation 9.x types
        // (referenced from ext/Lidarr/_output, NOT from NuGet FV 11).
        Integration.TidalarrSettings core = ToCore();
        (bool isValid, var errors) = core.ValidateSimple();

        FluentValidation.Results.ValidationResult fluent = new();
        foreach ((string property, string error) in errors)
        {
            fluent.Errors.Add(new FluentValidation.Results.ValidationFailure(property, error));
        }

        return new NzbDrone.Core.Validation.NzbDroneValidationResult(fluent);
    }

    // Map to core settings
    public Integration.TidalarrSettings ToCore()
    {
        return new Integration.TidalarrSettings
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
