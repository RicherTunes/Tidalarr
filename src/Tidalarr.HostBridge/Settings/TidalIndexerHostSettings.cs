using NzbDrone.Core.Annotations;

namespace Tidalarr.HostBridge.Settings;

public class TidalIndexerHostSettings
{
    [FieldDefinition(0, Label = "Config Path", Type = FieldType.Textbox, HelpText = "Directory used to persist Tidal authentication tokens.")]
    public string ConfigPath { get; set; } = string.Empty;

    [FieldDefinition(1, Label = "Redirect URL", Type = FieldType.Textbox, HelpText = "OAuth redirect URL captured after completing the Tidal login flow.")]
    public string RedirectUrl { get; set; } = string.Empty;

    [FieldDefinition(2, Label = "Market", Type = FieldType.Textbox, HelpText = "Two-letter Tidal market code (US, UK, DE, FR, CA, AU, JP).", Advanced = true)]
    public string TidalMarket { get; set; } = "US";
}

