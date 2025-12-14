using NzbDrone.Core.Indexers;

namespace Tidalarr;

// Host-facing protocol type kept in a dedicated bridge assembly to avoid pulling host dependencies into the core plugin package.
public class TidalProtocol : IDownloadProtocol
{
    public const string Scheme = "tidal";
    public const string Name = "TidalProtocol";
    public const string Description = "Tidal streaming protocol";

    public static bool IsValidUrl(string url)
    {
        return !string.IsNullOrEmpty(url) && url.StartsWith($"{Scheme}://", StringComparison.OrdinalIgnoreCase);
    }

    public static (string Type, string Id) ParseUrl(string url)
    {
        if (!IsValidUrl(url))
        {
            throw new ArgumentException($"Invalid Tidal URL: {url}");
        }

        string[] parts = url[(Scheme.Length + 3)..].Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length != 2 ? throw new ArgumentException($"Invalid Tidal URL format: {url}") : ((string Type, string Id))(parts[0], parts[1]);
    }

    public static string BuildAlbumUrl(string albumId)
    {
        string safe = albumId ?? string.Empty;
        return $"{Scheme}://album/{safe}";
    }

    public static string BuildTrackUrl(string trackId)
    {
        string safe = trackId ?? string.Empty;
        return $"{Scheme}://track/{safe}";
    }
}
