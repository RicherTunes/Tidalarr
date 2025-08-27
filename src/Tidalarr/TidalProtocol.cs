namespace Tidalarr;

public class TidalProtocol
{
    public const string Name = "TidalProtocol";
    public const string Description = "Tidal streaming protocol";
    
    public static bool IsValidUrl(string url)
    {
        return url.StartsWith("tidal://", StringComparison.OrdinalIgnoreCase);
    }
    
    public static (string type, string id) ParseUrl(string url)
    {
        if (!IsValidUrl(url))
            throw new ArgumentException($"Invalid Tidal URL: {url}");
            
        var parts = url.Substring(8).Split('/', 2); // Remove "tidal://"
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid Tidal URL format: {url}");
            
        return (parts[0], parts[1]);
    }
    
    public static string BuildAlbumUrl(string albumId) => $"tidal://album/{albumId}";
    public static string BuildTrackUrl(string trackId) => $"tidal://track/{trackId}";
}
