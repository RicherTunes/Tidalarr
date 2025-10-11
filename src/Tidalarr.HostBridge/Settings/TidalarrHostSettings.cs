using NzbDrone.Core.Annotations;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.ThingiProvider;

namespace Tidalarr.HostBridge.Settings;

// Host-only wrapper that carries NzbDrone UI annotations and maps to core settings
public class TidalarrHostSettings : IIndexerSettings, IProviderConfig
{
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
