using FluentValidation;
using Lidarr.Plugin.Common.Hosting;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Validation;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Which slice of the user's Tidal library the favorites import list mirrors into Lidarr.
/// </summary>
public enum TidalFavoritesContent
{
    /// <summary>Favorite albums and favorite artists (default).</summary>
    AlbumsAndArtists = 0,

    /// <summary>Favorite albums only.</summary>
    AlbumsOnly = 1,

    /// <summary>Favorite artists only.</summary>
    ArtistsOnly = 2
}

/// <summary>
/// Lidarr-native import-list settings implementing <see cref="IImportListSettings"/> for plugin
/// discovery. Provides UI fields visible in Lidarr's Settings &gt; Import Lists &gt; Add &gt; Tidalarr.
///
/// Authentication is shared with the Tidalarr indexer/download client: the OAuth token is read
/// from the same <c>ConfigPath</c> token store, so once the indexer is authenticated the import
/// list needs no separate login.
/// </summary>
public class TidalFavoritesImportListSettings : IImportListSettings
{
    private static readonly TidalFavoritesImportListSettingsValidator Validator = new();
    private static readonly string DefaultConfigPath = PluginConfigRoots.Resolve("Tidalarr");

    public TidalFavoritesImportListSettings()
    {
        ConfigPath = DefaultConfigPath;
        TidalMarket = "US";
        Content = TidalFavoritesContent.AlbumsAndArtists;
    }

    [FieldDefinition(0, Label = "Config Path", Type = FieldType.Path, Section = "Authentication",
        HelpText = "Directory holding the Tidal authentication tokens written by the Tidalarr indexer. " +
                   "Defaults to /config/Tidalarr in Docker, otherwise AppData/Tidalarr (~/.config/Tidalarr on Linux). " +
                   "Authenticate the Tidalarr indexer first — the import list reuses the same session.")]
    public string ConfigPath { get; set; }

    [FieldDefinition(1, Label = "Favorites To Import", Type = FieldType.Select, SelectOptions = typeof(TidalFavoritesContent),
        HelpText = "Which favorites to mirror: albums and artists, albums only, or artists only.")]
    public TidalFavoritesContent Content { get; set; }

    [FieldDefinition(2, Label = "Market", Type = FieldType.Textbox, Section = "Authentication", Advanced = true,
        HelpText = "Two-letter Tidal market code (US, GB, DE, FR, CA, AU, JP).")]
    public string TidalMarket { get; set; }

    /// <summary>
    /// <see cref="IImportListSettings"/> member. The Tidal favorites API base; not user-editable
    /// (auth is via the shared token store), but the host contract requires a non-empty base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.tidal.com";

    public NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult(Validator.Validate(this));
    }
}

public class TidalFavoritesImportListSettingsValidator : AbstractValidator<TidalFavoritesImportListSettings>
{
    public TidalFavoritesImportListSettingsValidator()
    {
        _ = RuleFor(x => x.ConfigPath)
            .NotEmpty().WithMessage(
                "Config path is required. Point it at the same directory the Tidalarr indexer uses " +
                "(default /config/Tidalarr in Docker) so the import list can read the OAuth token.");

        _ = RuleFor(x => x.TidalMarket)
            .Must(TidalMarket.IsValid)
            .WithMessage("Market must be a 2-letter ISO 3166-1 country code (e.g. US, GB, DE, FR, CA, AU, JP).");

        _ = RuleFor(x => x.Content)
            .Must(v => Enum.IsDefined(typeof(TidalFavoritesContent), v))
            .WithMessage("Select which favorites to import (albums and artists, albums only, or artists only).");
    }
}
