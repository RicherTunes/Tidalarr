using NzbDrone.Core.Annotations;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.ThingiProvider;

namespace Tidalarr.HostBridge.Settings;

// Host-only wrapper that carries NzbDrone UI annotations and maps to core settings
public class TidalarrHostSettings : IIndexerSettings, IProviderConfig
{
    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.ConfigPathOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.ConfigPathLabel, Type = FieldType.Textbox, HelpText = "Directory used to persist Tidal authentication tokens.")]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.RedirectUrlOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.RedirectUrlLabel, Type = FieldType.Textbox, HelpText = "OAuth redirect URL captured after completing the Tidal login flow.")]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.MarketOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.MarketLabel, Type = FieldType.Textbox, HelpText = "Two-letter Tidal market code (US, UK, DE, FR, CA, AU, JP).", Advanced = true)]
    public string TidalMarket { get; set; } = "US";

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.EarlyDownloadLimitOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.EarlyDownloadLimitLabel, Type = FieldType.Number, Unit = Tidalarr.Integration.SettingsDisplay.Indexer.EarlyDownloadLimitUnit, HelpText = "Limit pre-release downloads to this many days before release.", Advanced = true)]
    public int? EarlyReleaseLimit { get; set; } = 14;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.EnableCacheOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.EnableCacheLabel, Type = FieldType.Checkbox, Advanced = true)]
    public bool EnableCache { get; set; } = true;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.CacheDurationOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.CacheDurationLabel, Type = FieldType.Number, Unit = Tidalarr.Integration.SettingsDisplay.Indexer.CacheDurationUnit, Advanced = true)]
    public int CacheDuration { get; set; } = 15;

    public string BaseUrl { get; set; } = "https://api.tidal.com";

    public NzbDrone.Core.Validation.NzbDroneValidationResult Validate()
    {
        // Delegate to core validator to shape a host result
        var core = this.ToCore();
        var fluent = core.ValidateFluent();
        return new NzbDrone.Core.Validation.NzbDroneValidationResult(fluent);
    }

    // Map to core settings
    public Tidalarr.Integration.TidalarrSettings ToCore()
    {
        return new Tidalarr.Integration.TidalarrSettings
        {
            ConfigPath = this.ConfigPath,
            RedirectUrl = this.RedirectUrl,
            TidalMarket = this.TidalMarket,
            EarlyReleaseLimit = this.EarlyReleaseLimit,
            EnableCache = this.EnableCache,
            CacheDuration = this.CacheDuration
        };
    }
}
