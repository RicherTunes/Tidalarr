namespace Tidalarr.HostBridge.Settings;

// Host-only wrapper that carries NzbDrone UI annotations and maps to core settings
public class TidalarrHostSettings : IIndexerSettings, IProviderConfig
{
    [FieldDefinition(Integration.SettingsDisplay.Indexer.ConfigPathOrder, Label = Integration.SettingsDisplay.Indexer.ConfigPathLabel, Type = FieldType.Textbox, HelpText = "Directory used to persist Tidal authentication tokens.")]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.RedirectUrlOrder, Label = Integration.SettingsDisplay.Indexer.RedirectUrlLabel, Type = FieldType.Textbox, HelpText = "OAuth redirect URL captured after completing the Tidal login flow.")]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.MarketOrder, Label = Integration.SettingsDisplay.Indexer.MarketLabel, Type = FieldType.Textbox, HelpText = "Two-letter Tidal market code (US, UK, DE, FR, CA, AU, JP).", Advanced = true)]
    public string TidalMarket { get; set; } = "US";

    [FieldDefinition(Integration.SettingsDisplay.Indexer.EarlyDownloadLimitOrder, Label = Integration.SettingsDisplay.Indexer.EarlyDownloadLimitLabel, Type = FieldType.Number, Unit = Integration.SettingsDisplay.Indexer.EarlyDownloadLimitUnit, HelpText = "Limit pre-release downloads to this many days before release.", Advanced = true)]
    public int? EarlyReleaseLimit { get; set; } = 14;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.EnableCacheOrder, Label = Integration.SettingsDisplay.Indexer.EnableCacheLabel, Type = FieldType.Checkbox, Advanced = true)]
    public bool EnableCache { get; set; } = true;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.CacheDurationOrder, Label = Integration.SettingsDisplay.Indexer.CacheDurationLabel, Type = FieldType.Number, Unit = Integration.SettingsDisplay.Indexer.CacheDurationUnit, Advanced = true)]
    public int CacheDuration { get; set; } = 15;

    public string BaseUrl { get; set; } = "https://api.tidal.com";

    public NzbDrone.Core.Validation.NzbDroneValidationResult Validate()
    {
        // Delegate to core validator to shape a host result
        Integration.TidalarrSettings core = ToCore();
        FluentValidation.Results.ValidationResult fluent = core.ValidateFluent();
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
