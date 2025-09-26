using System;
using NzbDrone.Core.Indexers;

namespace Tidalarr;

public class TidalProtocol : IDownloadProtocol
{
    public const string Scheme = "tidal";
    public const string Name = "Tidal";
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

        var parts = url.Substring(Scheme.Length + 3).Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid Tidal URL format: {url}");
        }

        return (parts[0], parts[1]);
    }

    public static string BuildAlbumUrl(string albumId)
    {
        if (string.IsNullOrWhiteSpace(albumId))
        {
            throw new ArgumentException("Album id cannot be null or empty", nameof(albumId));
        }

        return $"{Scheme}://album/{albumId}";
    }

    public static string BuildTrackUrl(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            throw new ArgumentException("Track id cannot be null or empty", nameof(trackId));
        }

        return $"{Scheme}://track/{trackId}";
    }
}


