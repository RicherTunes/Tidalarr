using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Parser.Model;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Maps Tidal favorites (<see cref="TidalAlbumInfo"/> / <see cref="TidalArtistInfo"/>) to Lidarr
/// <see cref="ImportListItemInfo"/> entries.
///
/// Lidarr resolves import-list items to library entities by <b>name</b> (Artist / Artist+Album),
/// then matches to MusicBrainz — there is no Tidal-native catalog-id field on
/// <see cref="ImportListItemInfo"/>. An entry with an artist only adds/monitors that artist; an
/// entry with artist + album adds that album. Entries missing the essential name(s) are dropped
/// (they'd be un-resolvable noise), and duplicates are collapsed case-insensitively so the same
/// favorite can't enqueue twice.
/// </summary>
internal static class TidalFavoritesMapper
{
    public static List<ImportListItemInfo> Map(
        IReadOnlyList<TidalAlbumInfo>? albums,
        IReadOnlyList<TidalArtistInfo>? artists)
    {
        List<ImportListItemInfo> items = [];

        if (albums is not null)
        {
            foreach (TidalAlbumInfo album in albums)
            {
                string artist = PrimaryArtist(album);
                if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(album.Title))
                {
                    continue;
                }

                items.Add(new ImportListItemInfo
                {
                    Artist = artist.Trim(),
                    Album = album.Title.Trim(),
                    // ImportListItemInfo.ReleaseDate is a non-nullable DateTime; DateTime.MinValue
                    // (unknown) is the same sentinel Lidarr's own lists use.
                    ReleaseDate = album.ReleaseDate
                });
            }
        }

        if (artists is not null)
        {
            foreach (TidalArtistInfo artistInfo in artists)
            {
                if (string.IsNullOrWhiteSpace(artistInfo.Name))
                {
                    continue;
                }

                items.Add(new ImportListItemInfo { Artist = artistInfo.Name.Trim() });
            }
        }

        return Deduplicate(items);
    }

    private static string PrimaryArtist(TidalAlbumInfo album)
    {
        return album.Artists is { Count: > 0 } ? album.Artists[0] : string.Empty;
    }

    private static List<ImportListItemInfo> Deduplicate(List<ImportListItemInfo> items)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<ImportListItemInfo> result = [];
        foreach (ImportListItemInfo item in items)
        {
            string key = item.Artist + " " + item.Album;
            if (seen.Add(key))
            {
                result.Add(item);
            }
        }

        return result;
    }
}
