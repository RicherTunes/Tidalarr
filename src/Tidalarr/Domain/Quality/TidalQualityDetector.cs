using Tidalarr.Core.Models;

namespace Tidalarr.Domain.Quality;

public class TidalQualityDetector
{
    public TidalQuality DetectQualityFromString(string qualityString)
    {
        return qualityString?.ToUpperInvariant() switch
        {
            "LOW" => TidalQuality.Low,
            "HIGH" => TidalQuality.High,
            "LOSSLESS" => TidalQuality.Lossless,
            "HI_RES_LOSSLESS" => TidalQuality.HiRes,
            _ => TidalQuality.High // Default fallback
        };
    }

    public List<TidalQuality> DetectAvailableQualities(string[] tags)
    {
        List<TidalQuality> qualities =
        [
            // Always add basic qualities
            TidalQuality.Low,
            TidalQuality.High,
        ];

        // Check for lossless availability
        if (tags.Contains("LOSSLESS") || tags.Contains("HIRES_LOSSLESS"))
        {
            qualities.Add(TidalQuality.Lossless);
        }

        // Check for hi-res availability
        if (tags.Contains("HIRES_LOSSLESS"))
        {
            qualities.Add(TidalQuality.HiRes);
        }

        return [.. qualities.Distinct().OrderBy(q => (int)q)];
    }

    public TidalQuality SelectBestQuality(IEnumerable<TidalQuality> availableQualities, TidalQuality userPreference)
    {
        List<TidalQuality> available = availableQualities.ToList();

        if (!available.Any())
            return TidalQuality.High; // Fallback

        // If user preference is available, use it
        if (available.Contains(userPreference))
            return userPreference;

        // Find the highest quality that's not higher than user preference
        List<TidalQuality> suitableQualities = available.Where(q => q <= userPreference).ToList();
        if (suitableQualities.Any())
            return suitableQualities.Max();

        // If no suitable quality, use the lowest available (better than nothing)
        return available.Min();
    }

    public TidalQuality DetectHighestAvailableQuality(string[] tags)
    {
        List<TidalQuality> availableQualities = DetectAvailableQualities(tags);
        return availableQualities.Any() ? availableQualities.Max() : TidalQuality.High;
    }

    public bool IsQualityAvailable(TidalQuality quality, string[] tags)
    {
        List<TidalQuality> availableQualities = DetectAvailableQualities(tags);
        return availableQualities.Contains(quality);
    }

    public string GetQualityDisplayName(TidalQuality quality)
    {
        return quality switch
        {
            TidalQuality.Low => "Low Quality (96 kbps AAC)",
            TidalQuality.High => "High Quality (320 kbps AAC)",
            TidalQuality.Lossless => "Lossless (FLAC 16-bit/44.1kHz)",
            TidalQuality.HiRes => "Hi-Res (FLAC up to 24-bit/192kHz)",
            _ => "Unknown Quality"
        };
    }
}

