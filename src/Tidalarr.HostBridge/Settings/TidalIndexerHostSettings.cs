using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalIndexerHostSettings
{
    [FieldDefinition(Integration.SettingsDisplay.Indexer.ConfigPathOrder, Label = Integration.SettingsDisplay.Indexer.ConfigPathLabel, Type = FieldType.Textbox, HelpText = Integration.SettingsDisplay.Indexer.ConfigPathHelpText)]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.RedirectUrlOrder, Label = Integration.SettingsDisplay.Indexer.RedirectUrlLabel, Type = FieldType.Textbox, HelpText = Integration.SettingsDisplay.Indexer.RedirectUrlHelpText)]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(Integration.SettingsDisplay.Indexer.MarketOrder, Label = Integration.SettingsDisplay.Indexer.MarketLabel, Type = FieldType.Textbox, HelpText = Integration.SettingsDisplay.Indexer.MarketHelpText, Advanced = true)]
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
