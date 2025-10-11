using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalIndexerHostSettings
{
    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.ConfigPathOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.ConfigPathLabel, Type = FieldType.Textbox, HelpText = "Directory used to persist Tidal authentication tokens.")]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.RedirectUrlOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.RedirectUrlLabel, Type = FieldType.Textbox, HelpText = "OAuth redirect URL captured after completing the Tidal login flow.")]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(Tidalarr.Integration.SettingsDisplay.Indexer.MarketOrder, Label = Tidalarr.Integration.SettingsDisplay.Indexer.MarketLabel, Type = FieldType.Textbox, HelpText = "Two-letter Tidal market code (US, UK, DE, FR, CA, AU, JP).", Advanced = true)]
    public string TidalMarket { get; set; } = "US";

    public Tidalarr.Integration.TidalIndexerSettings ToCore()
    {
        return new Tidalarr.Integration.TidalIndexerSettings
        {
            ConfigPath = ConfigPath,
            RedirectUrl = RedirectUrl,
            TidalMarket = TidalMarket
        };
    }
}
