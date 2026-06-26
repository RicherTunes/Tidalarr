using System;
using System.Collections.Generic;
using System.Linq;
using Lidarr.Plugin.Common.Services.Intelligence;
using Xunit;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Special-character + fallback matrix for Tidal album/artist search. The variant generation and
/// fallback-tier ordering were consolidated onto Common's <see cref="SearchQuerySanitizer"/>
/// (the indexer's <c>TidalLidarrRequestGenerator</c> now calls
/// <see cref="SearchQuerySanitizer.BuildPlan(string, string, SanitizerOptions)"/>); these tests pin
/// the same tidal-relevant behavior against that canonical implementation so a Common change can't
/// silently regress Tidal search.
///
/// Live regression: an album search for "Bleu Jeans Bleu - Record n°V (2026)" returned 0 results
/// because the indexer issued a single over-specific combined query and a mid-token symbol (°) was
/// never stripped/varied. The plan must (a) emit recall-widening variants for special characters
/// without spurious token splits and (b) order fallback tiers so an empty combined query falls back
/// to artist-only.
/// </summary>
public sealed class TidalSearchPlanTests
{
    private static IReadOnlyList<string> Variants(string? raw) => SearchQuerySanitizer.Sanitize(raw).Variants;

    private static IReadOnlyList<IReadOnlyList<string>> Tiers(string? artist, string? album)
        => SearchQuerySanitizer.BuildPlan(artist, album).Tiers;

    [Fact]
    public void Variants_RecordNumeroV_StripsDegreeWithoutTokenSplit()
    {
        var variants = Variants("Record n°V");

        // The original is preserved (let the service normalize), AND a symbol-removed form
        // that keeps "nV" together (NOT "n V") is offered — qobuz's own slug is "record-nv".
        Assert.Contains("Record n°V", variants);
        Assert.Contains("Record nV", variants);
    }

    [Fact]
    public void Variants_AcDc_ProducesSpaceSeparatedForm()
    {
        var variants = Variants("AC/DC");

        // Separator must not be destroyed: "AC DC" is the expected recall-friendly form.
        Assert.Contains("AC/DC", variants);
        Assert.Contains("AC DC", variants);
    }

    [Fact]
    public void Variants_GunsNRoses_ApostropheDoesNotRegress()
    {
        var variants = Variants("Guns N' Roses");

        Assert.Contains("Guns N' Roses", variants);
        Assert.Contains("Guns N Roses", variants);
    }

    [Theory]
    [InlineData("P!nk", "Pnk")]
    [InlineData("Ke$ha", "Keha")]
    [InlineData("Panic! at the Disco", "Panic at the Disco")]
    public void Variants_MidTokenSymbol_OffersStrippedForm(string raw, string strippedExpected)
    {
        var variants = Variants(raw);

        Assert.Contains(raw, variants);
        Assert.Contains(strippedExpected, variants);
    }

    [Theory]
    [InlineData("Beyoncé", "Beyonce")]
    [InlineData("Motörhead", "Motorhead")]
    [InlineData("Sigur Rós", "Sigur Ros")]
    public void Variants_Accented_OffersFoldedForm(string raw, string foldedExpected)
    {
        var variants = Variants(raw);

        Assert.Contains(raw, variants);
        Assert.Contains(foldedExpected, variants);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("+/-")]
    public void Variants_AllSymbolBandName_HasNoSignal_RoutesToAlias(string raw)
    {
        // Canonical Common contract: a symbol-only title carries no usable search signal, so it
        // yields no variants and flags NeedsAlias (the caller must artist-scope / alias-resolve it
        // rather than issue a hopeless symbol-only query). This supersedes the old tidal-local
        // behavior that kept the literal "!!!" as a variant.
        var result = SearchQuerySanitizer.Sanitize(raw);

        Assert.False(result.HasSignal);
        Assert.True(result.NeedsAlias);
        Assert.Empty(result.Variants);
    }

    [Fact]
    public void Variants_PlainAscii_NoRegression_SingleVariant()
    {
        var variants = Variants("Daft Punk Discovery");

        Assert.Single(variants);
        Assert.Equal("Daft Punk Discovery", variants[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Variants_EmptyOrWhitespace_ReturnsEmpty(string? raw)
    {
        Assert.Empty(Variants(raw));
    }

    [Fact]
    public void Variants_AreDistinct()
    {
        var variants = Variants("Guns N' Roses");
        Assert.Equal(variants.Count, variants.Distinct().Count());
    }

    // ---- Tier / fallback ordering ----------------------------------------

    [Fact]
    public void BuildPlan_CombinedThenArtistOnlyFallback()
    {
        var tiers = Tiers("Bleu Jeans Bleu", "Record n°V");

        Assert.True(tiers.Count >= 2, "expected at least a combined tier and an artist-only fallback tier");

        // Tier 0 = combined artist + album (with variants).
        Assert.Contains("Bleu Jeans Bleu Record n°V", tiers[0]);
        Assert.Contains("Bleu Jeans Bleu Record nV", tiers[0]);

        // A later tier carries the FULL (never-truncated) artist-only fallback so Lidarr still
        // receives the band catalog.
        Assert.Contains(tiers.Skip(1), tier => tier.Contains("Bleu Jeans Bleu"));
    }

    [Fact]
    public void BuildPlan_FirstTierIsCombined_NotArtistOnly()
    {
        var tiers = Tiers("Daft Punk", "Discovery");

        Assert.NotEmpty(tiers);
        Assert.Contains("Daft Punk Discovery", tiers[0]);
        // Artist-only must NOT be the first thing attempted — it is a fallback only.
        Assert.DoesNotContain(tiers[0], q => q == "Daft Punk");
    }

    [Fact]
    public void BuildPlan_IncludesAlbumOnlyFallback_WhenBothPresent()
    {
        var tiers = Tiers("Daft Punk", "Discovery");

        Assert.Contains(tiers.Skip(1), tier => tier.Contains("Discovery"));
    }

    [Fact]
    public void BuildPlan_AlbumEmpty_OnlyOneTier_NoRedundantArtistFallback()
    {
        var tiers = Tiers("Daft Punk", "");

        Assert.Single(tiers);
        Assert.Contains("Daft Punk", tiers[0]);
    }

    [Fact]
    public void BuildPlan_BothEmpty_NoTiers()
    {
        Assert.Empty(Tiers("", ""));
        Assert.Empty(Tiers(null, null));
    }

    [Fact]
    public void BuildPlan_AccentedArtist_FoldedVariantInArtistFallback()
    {
        var tiers = Tiers("Motörhead", "Ace of Spades");

        // The artist-only fallback tier should carry both the literal and accent-folded artist.
        var fallbackTiers = tiers.Skip(1).ToList();
        Assert.Contains(fallbackTiers, tier => tier.Contains("Motörhead"));
        Assert.Contains(fallbackTiers, tier => tier.Contains("Motorhead"));
    }
}
