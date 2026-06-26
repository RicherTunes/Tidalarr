using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Builds the search terms the Tidal indexer issues for an album/artist search.
///
/// <para>Two responsibilities:</para>
/// <list type="number">
///   <item><b>Special-character variants.</b> A single over-specific query loses albums whose
///   titles carry symbols the service normalizes differently (e.g. "Record n°V" — qobuz's own
///   slug for it is <c>record-nv</c>, i.e. the degree sign is dropped, NOT turned into a space).
///   Rather than apply one lossy transform, <see cref="GenerateVariants"/> emits a small ordered
///   set of variants — the original, a symbol-removed form that keeps tokens intact
///   (<c>n°V</c> → <c>nV</c>), a separator-friendly form (<c>AC/DC</c> → <c>AC DC</c>), and an
///   accent-folded form (<c>Motörhead</c> → <c>Motorhead</c>) — so the indexer can try them all.</item>
///   <item><b>Fallback tiers.</b> <see cref="BuildTiers"/> orders the work as combined
///   "artist album" first, then artist-only, then album-only. The indexer runs them in order and
///   stops at the first tier that yields results, so an over-specific combined query that finds
///   nothing falls back to returning the band's catalogue for Lidarr's decision engine to match.</item>
/// </list>
///
/// <para>Deliberately kept dependency-free and Tidal-local. A natural future home for the
/// variant generation is <c>Lidarr.Plugin.Common</c> (every streaming plugin has the same bug
/// class), but it is left here until the qobuz-side fix lands so the two can be reconciled.</para>
/// </summary>
internal static class TidalSearchTermBuilder
{
    private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Returns the ordered, distinct, non-empty search-term variants for a single raw term.
    /// Order: original, symbol-stripped (no token split), symbols-to-space, accent-folded.
    /// Empty / whitespace / null input yields an empty list.
    /// </summary>
    internal static IReadOnlyList<string> GenerateVariants(string? raw)
    {
        string original = CollapseWhitespace(raw);
        if (original.Length == 0)
        {
            return [];
        }

        List<string> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string candidate)
        {
            string normalized = CollapseWhitespace(candidate);
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        TryAdd(original);
        TryAdd(StripSymbols(original));
        TryAdd(SymbolsToSpace(original));
        TryAdd(RemoveDiacritics(original));

        return result;
    }

    /// <summary>
    /// Returns the ordered search tiers for an album search: combined "artist album" first,
    /// then artist-only, then album-only. Each tier is the variant set for that term. Tiers
    /// that add no new signal (e.g. artist-only when the album is empty) are omitted, and an
    /// empty result means there is nothing to search.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<string>> BuildTiers(string? artistQuery, string? albumQuery)
    {
        string artist = CollapseWhitespace(artistQuery);
        string album = CollapseWhitespace(albumQuery);
        string combined = CollapseWhitespace(artist + " " + album);

        List<IReadOnlyList<string>> tiers = [];

        IReadOnlyList<string> combinedVariants = GenerateVariants(combined);
        if (combinedVariants.Count > 0)
        {
            tiers.Add(combinedVariants);
        }

        // Artist-only and album-only fallbacks only add value when BOTH parts are present —
        // otherwise the combined tier already IS the artist-only (or album-only) query.
        if (artist.Length > 0 && album.Length > 0)
        {
            IReadOnlyList<string> artistVariants = GenerateVariants(artist);
            if (artistVariants.Count > 0)
            {
                tiers.Add(artistVariants);
            }

            IReadOnlyList<string> albumVariants = GenerateVariants(album);
            if (albumVariants.Count > 0)
            {
                tiers.Add(albumVariants);
            }
        }

        return tiers;
    }

    private static string CollapseWhitespace(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : MultiWhitespace.Replace(value, " ").Trim();

    private static bool IsKeep(char c) => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c);

    // Drops every non-alphanumeric, non-whitespace character WITHOUT inserting a space, so a
    // mid-token symbol collapses the token rather than splitting it ("n°V" -> "nV", "AC/DC" -> "ACDC").
    private static string StripSymbols(string value)
    {
        StringBuilder sb = new(value.Length);
        foreach (char c in value)
        {
            if (IsKeep(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    // Replaces every non-alphanumeric, non-whitespace character with a space — the right move for
    // true separators ("AC/DC" -> "AC DC") even though it splits mid-token symbols.
    private static string SymbolsToSpace(string value)
    {
        StringBuilder sb = new(value.Length);
        foreach (char c in value)
        {
            sb.Append(IsKeep(c) ? c : ' ');
        }

        return sb.ToString();
    }

    // Strips combining diacritical marks ("Motörhead" -> "Motorhead", "Beyoncé" -> "Beyonce").
    private static string RemoveDiacritics(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
