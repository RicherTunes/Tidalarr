using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalIndexerHostSettings
{
    [FieldDefinition(Integration.SettingsDisplay.Indexer.ConfigPathOrder, Label = Integration.SettingsDisplay.Indexer.ConfigPathLabel, Type = FieldType.Textbox, HelpText = "Directory used to persist Tidal authentication tokens.")]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.RedirectUrlOrder, Label = Integration.SettingsDisplay.Indexer.RedirectUrlLabel, Type = FieldType.Textbox, HelpText = "OAuth redirect URL captured after completing the Tidal login flow.")]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.MarketOrder, Label = Integration.SettingsDisplay.Indexer.MarketLabel, Type = FieldType.Textbox, HelpText = "Two-letter Tidal market code (US, UK, DE, FR, CA, AU, JP).", Advanced = true)]
    public string TidalMarket { get; set; } = "US";

    public Integration.TidalIndexerSettings ToCore()
    {
        return new Integration.TidalIndexerSettings
        {
            ConfigPath = ConfigPath,
            RedirectUrl = RedirectUrl,
            TidalMarket = TidalMarket
        };
    }
}
