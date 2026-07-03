namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Normalizes a user-entered Tidal market to an ISO 3166-1 alpha-2 country code. Tidal's API expects a
/// 2-letter ISO code; the common "UK" mistake must map to ISO "GB" (Tidal rejects "UK"). Empty/invalid
/// input falls back to <see cref="Default"/>.
/// </summary>
public static class TidalMarket
{
    public const string Default = "US";

    /// <summary>Trim + uppercase, map UK -> GB, fall back to US for empty/non-2-letter input.</summary>
    public static string Normalize(string? market)
    {
        if (string.IsNullOrWhiteSpace(market))
        {
            return Default;
        }

        var m = market.Trim().ToUpperInvariant();
        if (m == "UK")
        {
            return "GB";
        }

        return IsTwoAsciiLetters(m) ? m : Default;
    }

    /// <summary>
    /// True when <paramref name="market"/> is a 2-letter code (accepts the "UK" alias, which
    /// <see cref="Normalize"/> rewrites to GB). Used by the settings validator.
    /// </summary>
    public static bool IsValid(string? market)
        => !string.IsNullOrWhiteSpace(market) && IsTwoAsciiLetters(market.Trim());

    private static bool IsTwoAsciiLetters(string m)
        => m.Length == 2 && char.IsAsciiLetter(m[0]) && char.IsAsciiLetter(m[1]);
}
