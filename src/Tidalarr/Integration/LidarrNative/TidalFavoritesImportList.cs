using FluentValidation.Results;
using Lidarr.Plugin.Common.Observability;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Lidarr-native import list that mirrors the authenticated user's Tidal favorites (favorite
/// albums and/or favorite artists) into Lidarr. First streaming-catalog import list in the
/// ecosystem.
///
/// Discovery: inheriting <see cref="ImportListBase{TSettings}"/> is enough for Lidarr's DryIoc
/// host to auto-construct it (same mechanism as the indexer/download client). Authentication is
/// shared with the Tidalarr indexer via the <c>ConfigPath</c> token store — no separate login.
/// </summary>
public class TidalFavoritesImportList(
    IImportListStatusService importListStatusService,
    IConfigService configService,
    IParsingService parsingService,
    Logger logger)
    : ImportListBase<TidalFavoritesImportListSettings>(importListStatusService, configService, parsingService, logger)
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);

    private new readonly Logger _logger = logger;

    public override string Name => "Tidalarr Favorites";

    public override ImportListType ListType => ImportListType.Other;

    public override TimeSpan MinRefreshInterval => RefreshInterval;

    public override IList<ImportListItemInfo> Fetch()
    {
        using PluginLogContext ctx = PluginLogContext.Push("Tidalarr", "ImportListSync", provider: "tidal:api");

        // SYNC-OVER-ASYNC: ImportListBase.Fetch() is a synchronous host contract. Task.Run avoids
        // deadlock if a SynchronizationContext captures the calling thread (mirrors the indexer's
        // EnsureServicesInitialized shim).
        IList<ImportListItemInfo> items = Task.Run(() => FetchInternalAsync()).GetAwaiter().GetResult();
        return CleanupListItems(items);
    }

    protected override void Test(List<ValidationFailure> failures)
    {
        using PluginLogContext ctx = PluginLogContext.Push("Tidalarr", "ImportListTest", provider: "tidal:api");
        Task.Run(() => TestInternalAsync(failures)).GetAwaiter().GetResult();
    }

    private async Task<IList<ImportListItemInfo>> FetchInternalAsync(CancellationToken cancellationToken = default)
    {
        TidalIndexerRuntime? runtime = await TidalImportListRuntimeCache.Shared.GetAsync(Settings, cancellationToken).ConfigureAwait(false);
        if (runtime is null)
        {
            this._logger.Error("Tidal favorites import list runtime unavailable (Config Path empty?)");
            return [];
        }

        ITidalCore? api = runtime.ServiceProvider.GetService<ITidalCore>();
        if (api is null)
        {
            this._logger.Error("Tidal favorites import list: ITidalCore not available");
            return [];
        }

        try
        {
            IList<ImportListItemInfo> items = await FetchFavoritesAsync(api, Settings.Content, this._logger, cancellationToken).ConfigureAwait(false);
            this._logger.Info($"Tidal favorites import list fetched {items.Count} item(s)");
            return items;
        }
        catch (Exception ex)
        {
            // Import lists must not throw out of Fetch(): the host would surface a raw stack trace and
            // clear previously-imported items. Log and return empty so the run is a no-op instead.
            this._logger.Error(ex, "Tidal favorites import list fetch failed");
            return [];
        }
    }

    private async Task TestInternalAsync(List<ValidationFailure> failures, CancellationToken cancellationToken = default)
    {
        TidalIndexerRuntime? runtime = await TidalImportListRuntimeCache.Shared.GetAsync(Settings, cancellationToken).ConfigureAwait(false);
        if (runtime is null)
        {
            failures.Add(new ValidationFailure(nameof(Settings.ConfigPath),
                "Config Path is empty. Set it to the directory the Tidalarr indexer uses so the import list can read the OAuth token."));
            return;
        }

        ITidalAuth? auth = runtime.ServiceProvider.GetService<ITidalAuth>();
        if (auth is null)
        {
            failures.Add(new ValidationFailure(string.Empty, "Tidal authentication service unavailable."));
            return;
        }

        await ValidateAuthAsync(auth, failures, this._logger, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches the selected favorites slice and maps it to import-list items. Internal + static so
    /// it can be unit-tested against a stub <see cref="ITidalCore"/> without the host runtime.
    /// </summary>
    internal static async Task<IList<ImportListItemInfo>> FetchFavoritesAsync(
        ITidalCore api,
        TidalFavoritesContent content,
        Logger? logger = null,
        CancellationToken cancellationToken = default)
    {
        List<TidalAlbumInfo>? albums = null;
        List<TidalArtistInfo>? artists = null;

        if (content is TidalFavoritesContent.AlbumsAndArtists or TidalFavoritesContent.AlbumsOnly)
        {
            albums = await api.GetFavoriteAlbumsAsync(cancellationToken).ConfigureAwait(false);
        }

        if (content is TidalFavoritesContent.AlbumsAndArtists or TidalFavoritesContent.ArtistsOnly)
        {
            artists = await api.GetFavoriteArtistsAsync(cancellationToken).ConfigureAwait(false);
        }

        return TidalFavoritesMapper.Map(albums, artists);
    }

    /// <summary>
    /// Validates that the session is authenticated with a real Tidal user id. Internal + static so
    /// it can be unit-tested against a stub <see cref="ITidalAuth"/>. Adds an actionable
    /// <see cref="ValidationFailure"/> (never throws) on any auth problem.
    /// </summary>
    internal static async Task ValidateAuthAsync(
        ITidalAuth auth,
        List<ValidationFailure> failures,
        Logger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            TidalTokens tokens = await auth.GetValidTokensAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tokens.UserId))
            {
                failures.Add(new ValidationFailure(nameof(TidalFavoritesImportListSettings.ConfigPath),
                    "Not authenticated with Tidal (no user id in the stored session). Configure and authenticate the " +
                    "Tidalarr indexer first (paste a fresh OAuth redirect URL), then re-test — the import list reuses that session."));
            }
        }
        catch (Exception ex)
        {
            logger?.Warn(ex, "Tidal favorites import list auth validation failed");
            failures.Add(new ValidationFailure(nameof(TidalFavoritesImportListSettings.ConfigPath),
                $"Could not authenticate with Tidal: {ex.Message}. Authenticate the Tidalarr indexer first, then re-test."));
        }
    }
}
