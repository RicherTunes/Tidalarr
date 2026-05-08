using Lidarr.Plugin.Abstractions.Models;
using Lidarr.Plugin.Common.Services.Quality;
using Lidarr.Plugin.Common.TestKit.Builders;

namespace Tidalarr.Tests;

/// <summary>
/// Coverage tests for QualityMapper.
/// Tests uncovered paths: null inputs, edge cases, all switch branches, comparison logic.
/// </summary>
public class TidalQualityMapperCovTests
{
    #region GetQualityTier - Null Input

    [Fact]
    public void GetQualityTier_NullQuality_ReturnsLowTier()
    {
        // Act - line 76: null quality returns StreamingQualityTier.Low
        StreamingQualityTier result = QualityMapper.GetQualityTier(null!);

        // Assert
        Assert.Equal(StreamingQualityTier.Low, result);
    }

    #endregion

    #region GetQualityTier - IsHighResolution Branch

    [Fact]
    public void GetQualityTier_HiResQuality_ReturnsHiResTier()
    {
        // Arrange - Hi-Res quality triggers line 79: IsHighResolution check
        StreamingQuality quality = StreamingQualityBuilder.CreateFlacHiRes();

        // Act
        StreamingQualityTier result = QualityMapper.GetQualityTier(quality);

        // Assert
        Assert.Equal(StreamingQualityTier.HiRes, result);
    }

    #endregion

    #region GetQualityTier - IsLossless Branch

    [Fact]
    public void GetQualityTier_LosslessQuality_ReturnsLosslessTier()
    {
        // Arrange - Lossless (but not HiRes) triggers line 83: IsLossless check
        StreamingQuality quality = StreamingQualityBuilder.CreateFlacCd();

        // Act
        StreamingQualityTier result = QualityMapper.GetQualityTier(quality);

        // Assert
        Assert.Equal(StreamingQualityTier.Lossless, result);
    }

    #endregion

    #region GetQualityTier - Bitrate Branches

    [Fact]
    public void GetQualityTier_HighBitrate320_ReturnsHighTier()
    {
        // Arrange - 320kbps triggers line 89: Bitrate >= 320
        StreamingQuality quality = StreamingQualityBuilder.CreateMp3320();

        // Act
        StreamingQualityTier result = QualityMapper.GetQualityTier(quality);

        // Assert
        Assert.Equal(StreamingQualityTier.High, result);
    }

    [Fact]
    public void GetQualityTier_NormalBitrate160_ReturnsNormalTier()
    {
        // Arrange - 160kbps triggers line 90: Bitrate >= 160
        StreamingQuality quality = new StreamingQualityBuilder()
            .WithFormat("AAC")
            .WithBitrate(160)
            .WithSampleRate(null)
            .WithBitDepth(null)
            .Build();

        // Act
        StreamingQualityTier result = QualityMapper.GetQualityTier(quality);

        // Assert
        Assert.Equal(StreamingQualityTier.Normal, result);
    }

    [Fact]
    public void GetQualityTier_LowBitrate_ReturnsLowTier()
    {
        // Arrange - 128kbps triggers line 91: return Low
        StreamingQuality quality = new StreamingQualityBuilder()
            .WithFormat("MP3")
            .WithBitrate(128)
            .WithSampleRate(null)
            .WithBitDepth(null)
            .Build();

        // Act
        StreamingQualityTier result = QualityMapper.GetQualityTier(quality);

        // Assert
        Assert.Equal(StreamingQualityTier.Low, result);
    }

    [Fact]
    public void GetQualityTier_NoBitrate_ReturnsNormalDefault()
    {
        // Arrange - no bitrate triggers line 94: default fallback to Normal
        StreamingQuality quality = new StreamingQualityBuilder()
            .WithFormat("Unknown")
            .WithBitrate(null)
            .WithSampleRate(null)
            .WithBitDepth(null)
            .Build();

        // Act
        StreamingQualityTier result = QualityMapper.GetQualityTier(quality);

        // Assert
        Assert.Equal(StreamingQualityTier.Normal, result);
    }

    #endregion

    #region FindBestMatch - Null/Empty Collection

    [Fact]
    public void FindBestMatch_NullCollection_ReturnsNull()
    {
        // Act - line 104: null collection returns null
        StreamingQuality? result = QualityMapper.FindBestMatch(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindBestMatch_EmptyCollection_ReturnsNull()
    {
        // Act - line 104: empty collection returns null
        StreamingQuality? result = QualityMapper.FindBestMatch([]);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region FindBestMatch - Exact Match

    [Fact]
    public void FindBestMatch_ExactTierMatch_ReturnsBestFromTier()
    {
        // Arrange - Lossless preference with Lossless available
        StreamingQuality flacCd = StreamingQualityBuilder.CreateFlacCd();
        StreamingQuality flacHiRes = StreamingQualityBuilder.CreateFlacHiRes();

        // Act - line 111-113: exact tier match
        StreamingQuality? result = QualityMapper.FindBestMatch([flacCd, flacHiRes], StreamingQualityTier.Lossless);

        // Assert - should return the FLAC CD (exact match)
        Assert.NotNull(result);
        Assert.Equal(StreamingQualityTier.Lossless, QualityMapper.GetQualityTier(result));
    }

    #endregion

    #region FindBestMatch - Higher Quality Search

    [Fact]
    public void FindBestMatch_NoExactMatch_SearchesHigherTiers()
    {
        // Arrange - want Normal, only have Lossless (higher)
        StreamingQuality flacCd = StreamingQualityBuilder.CreateFlacCd();

        // Act - lines 117-124: search higher tiers when no exact match
        StreamingQuality? result = QualityMapper.FindBestMatch([flacCd], StreamingQualityTier.Normal);

        // Assert - should find Lossless (higher tier)
        Assert.NotNull(result);
        Assert.Equal(StreamingQualityTier.Lossless, QualityMapper.GetQualityTier(result));
    }

    #endregion

    #region FindBestMatch - Lower Quality Search

    [Fact]
    public void FindBestMatch_NoHigherAvailable_SearchesLowerTiers()
    {
        // Arrange - want HiRes, only have Normal (lower)
        StreamingQuality mp3 = new StreamingQualityBuilder()
            .WithFormat("MP3")
            .WithBitrate(200)
            .WithSampleRate(null)
            .WithBitDepth(null)
            .Build();

        // Act - lines 127-133: search lower tiers when no higher available
        StreamingQuality? result = QualityMapper.FindBestMatch([mp3], StreamingQualityTier.HiRes);

        // Assert - should find Normal (lower tier)
        Assert.NotNull(result);
        Assert.Equal(StreamingQualityTier.Normal, QualityMapper.GetQualityTier(result));
    }

    #endregion

    #region FindBestMatch - Fallback

    [Fact]
    public void FindBestMatch_NoPreferredTiers_ReturnsAnyAvailable()
    {
        // Arrange - use Low preference with only High available (falls through to line 137)
        StreamingQuality mp3320 = StreamingQualityBuilder.CreateMp3320();

        // Act - line 137: fallback to any available quality
        StreamingQuality? result = QualityMapper.FindBestMatch([mp3320], StreamingQualityTier.Low);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(StreamingQualityTier.High, QualityMapper.GetQualityTier(result));
    }

    #endregion

    #region GetBestFromTier - Multiple Qualities

    [Fact]
    public void FindBestMatch_MultipleInSameTier_ReturnsBestBySpecs()
    {
        // Arrange - two HiRes qualities with different specs
        StreamingQuality flacHiRes = StreamingQualityBuilder.CreateFlacHiRes(); // 96kHz/24bit
        StreamingQuality flacUltra = StreamingQualityBuilder.CreateFlacUltraHiRes(); // 192kHz/24bit

        // Act - lines 148-152: GetBestFromTier orders by BitDepth, SampleRate, Bitrate
        StreamingQuality? result = QualityMapper.FindBestMatch([flacHiRes, flacUltra], StreamingQualityTier.HiRes);

        // Assert - should return the higher-spec quality (Ultra HiRes)
        Assert.NotNull(result);
        Assert.Equal(192000, result.SampleRate);
    }

    #endregion

    #region CompareQualities - Null Handling

    [Fact]
    public void CompareQualities_BothNull_ReturnsZero()
    {
        // Act - line 160: both null returns 0
        int result = QualityMapper.CompareQualities(null, null);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void CompareQualities_FirstNull_ReturnsNegativeOne()
    {
        // Act - line 161: quality1 null returns -1
        int result = QualityMapper.CompareQualities(null, StreamingQualityBuilder.CreateFlacCd());

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void CompareQualities_SecondNull_ReturnsOne()
    {
        // Act - line 162: quality2 null returns 1
        int result = QualityMapper.CompareQualities(StreamingQualityBuilder.CreateFlacCd(), null);

        // Assert
        Assert.Equal(1, result);
    }

    #endregion

    #region CompareQualities - Same Tier Comparison

    [Fact]
    public void CompareQualities_SameTier_CompareByBitDepth()
    {
        // Arrange - both HiRes, different bit depths
        StreamingQuality quality1 = new StreamingQualityBuilder()
            .WithFormat("FLAC")
            .WithSampleRate(96000)
            .WithBitDepth(16)
            .Build();
        StreamingQuality quality2 = StreamingQualityBuilder.CreateFlacHiRes(); // 24-bit

        // Act - line 172: compare by bit depth within same tier
        int result = QualityMapper.CompareQualities(quality1, quality2);

        // Assert - quality1 (16-bit) < quality2 (24-bit)
        Assert.True(result < 0);
    }

    [Fact]
    public void CompareQualities_SameTierAndBitDepth_CompareBySampleRate()
    {
        // Arrange - same bit depth, different sample rates
        StreamingQuality quality1 = new StreamingQualityBuilder()
            .WithFormat("FLAC")
            .WithSampleRate(48000)
            .WithBitDepth(24)
            .Build();
        StreamingQuality quality2 = StreamingQualityBuilder.CreateFlacHiRes(); // 96000

        // Act - line 175-176: compare by sample rate when bit depth equal
        int result = QualityMapper.CompareQualities(quality1, quality2);

        // Assert - quality1 (48kHz) < quality2 (96kHz)
        Assert.True(result < 0);
    }

    [Fact]
    public void CompareQualities_SameTierAndSpecs_CompareByBitrate()
    {
        // Arrange - same specs, different bitrates
        StreamingQuality quality1 = new StreamingQualityBuilder()
            .WithFormat("MP3")
            .WithBitrate(256)
            .WithSampleRate(null)
            .WithBitDepth(null)
            .Build();
        StreamingQuality quality2 = StreamingQualityBuilder.CreateMp3320(); // 320

        // Act - line 178: compare by bitrate as final tiebreaker
        int result = QualityMapper.CompareQualities(quality1, quality2);

        // Assert - quality1 (256) < quality2 (320)
        Assert.True(result < 0);
    }

    #endregion

    #region CreatePreferenceMap

    [Fact]
    public void CreatePreferenceMap_Default_AllowsHigherAndLower()
    {
        // Act - line 184-193: create preference map with defaults
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless);

        // Assert
        Assert.Equal(StreamingQualityTier.Lossless, map.PreferredTier);
        Assert.True(map.AllowHigherQuality);
        Assert.True(map.AllowLowerQuality);
        Assert.Equal(StreamingQualityTier.HiRes, map.MaxAcceptableTier);
        Assert.Equal(StreamingQualityTier.Low, map.MinAcceptableTier);
    }

    [Fact]
    public void CreatePreferenceMap_DisallowHigher_SetsMaxToPreferred()
    {
        // Act - line 191: allowHigher=false sets MaxAcceptableTier to preferred
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless, allowHigher: false);

        // Assert
        Assert.Equal(StreamingQualityTier.Lossless, map.MaxAcceptableTier);
    }

    [Fact]
    public void CreatePreferenceMap_DisallowLower_SetsMinToPreferred()
    {
        // Act - line 192: allowLower=false sets MinAcceptableTier to preferred
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless, allowLower: false);

        // Assert
        Assert.Equal(StreamingQualityTier.Lossless, map.MinAcceptableTier);
    }

    #endregion

    #region FromNumericId - Known IDs

    [Fact]
    public void FromNumericId_Id5_ReturnsMp3320()
    {
        // Act - line 205: qualityId 5 -> MP3 320kbps
        StreamingQuality result = QualityMapper.FromNumericId(5);

        // Assert
        Assert.Equal("5", result.Id);
        Assert.Equal("MP3 320kbps", result.Name);
        Assert.Equal("MP3", result.Format);
        Assert.Equal(320, result.Bitrate);
    }

    [Fact]
    public void FromNumericId_Id6_ReturnsFlacCd()
    {
        // Act - line 206: qualityId 6 -> FLAC CD
        StreamingQuality result = QualityMapper.FromNumericId(6);

        // Assert
        Assert.Equal("6", result.Id);
        Assert.Equal("FLAC CD", result.Name);
        Assert.Equal("FLAC", result.Format);
        Assert.Equal(44100, result.SampleRate);
        Assert.Equal(16, result.BitDepth);
    }

    [Fact]
    public void FromNumericId_Id7_ReturnsFlacHiRes()
    {
        // Act - line 207: qualityId 7 -> FLAC Hi-Res
        StreamingQuality result = QualityMapper.FromNumericId(7);

        // Assert
        Assert.Equal("7", result.Id);
        Assert.Equal("FLAC Hi-Res", result.Name);
        Assert.Equal(96000, result.SampleRate);
        Assert.Equal(24, result.BitDepth);
    }

    [Fact]
    public void FromNumericId_Id27_ReturnsFlacStudioMaster()
    {
        // Act - line 208: qualityId 27 -> FLAC Studio Master
        StreamingQuality result = QualityMapper.FromNumericId(27);

        // Assert
        Assert.Equal("27", result.Id);
        Assert.Equal("FLAC Studio Master", result.Name);
        Assert.Equal(192000, result.SampleRate);
        Assert.Equal(24, result.BitDepth);
    }

    #endregion

    #region FromNumericId - Unknown ID

    [Fact]
    public void FromNumericId_UnknownId_ReturnsGenericQuality()
    {
        // Act - line 209: unknown ID creates generic quality
        StreamingQuality result = QualityMapper.FromNumericId(999, "Tidal");

        // Assert
        Assert.Equal("999", result.Id);
        Assert.Equal("Tidal Quality 999", result.Name);
        Assert.Equal("Unknown", result.Format);
    }

    #endregion

    #region FromStringDescriptor - Null/Empty

    [Fact]
    public void FromStringDescriptor_Null_ReturnsNull()
    {
        // Act - line 219: null descriptor returns null
        StreamingQuality? result = QualityMapper.FromStringDescriptor(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FromStringDescriptor_Empty_ReturnsNull()
    {
        // Act - line 219: empty descriptor returns null
        StreamingQuality? result = QualityMapper.FromStringDescriptor(string.Empty);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region FromStringDescriptor - Known Descriptors

    [Fact]
    public void FromStringDescriptor_Low_ReturnsMp3Low()
    {
        // Act - line 225: "low" -> Mp3Low
        StreamingQuality? result = QualityMapper.FromStringDescriptor("low");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("mp3_128", result.Id);
        Assert.Equal(128, result.Bitrate);
    }

    [Fact]
    public void FromStringDescriptor_Normal_ReturnsMp3Low()
    {
        // Act - line 225: "normal" -> Mp3Low
        StreamingQuality? result = QualityMapper.FromStringDescriptor("normal");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("mp3_128", result.Id);
    }

    [Fact]
    public void FromStringDescriptor_High_ReturnsMp3High()
    {
        // Act - line 226: "high" -> Mp3High
        StreamingQuality? result = QualityMapper.FromStringDescriptor("high");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("mp3_320", result.Id);
        Assert.Equal(320, result.Bitrate);
    }

    [Fact]
    public void FromStringDescriptor_Lossless_ReturnsFlacCd()
    {
        // Act - line 227: "lossless" -> FlacCD
        StreamingQuality? result = QualityMapper.FromStringDescriptor("lossless");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("flac_cd", result.Id);
    }

    [Fact]
    public void FromStringDescriptor_Master_ReturnsFlacHiRes()
    {
        // Act - line 228: "master" -> FlacHiRes
        StreamingQuality? result = QualityMapper.FromStringDescriptor("master");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("flac_hires", result.Id);
    }

    [Fact]
    public void FromStringDescriptor_HiRes_ReturnsFlacHiRes()
    {
        // Act - line 228: "hi_res" -> FlacHiRes
        StreamingQuality? result = QualityMapper.FromStringDescriptor("hi_res");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("flac_hires", result.Id);
    }

    [Fact]
    public void FromStringDescriptor_Hires_ReturnsFlacHiRes()
    {
        // Act - line 228: "hires" -> FlacHiRes
        StreamingQuality? result = QualityMapper.FromStringDescriptor("hires");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("flac_hires", result.Id);
    }

    [Fact]
    public void FromStringDescriptor_StudioMaster_ReturnsFlacMax()
    {
        // Act - line 229: "studio_master" -> FlacMax
        StreamingQuality? result = QualityMapper.FromStringDescriptor("studio_master");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("flac_max", result.Id);
    }

    [Fact]
    public void FromStringDescriptor_Max_ReturnsFlacMax()
    {
        // Act - line 229: "max" -> FlacMax
        StreamingQuality? result = QualityMapper.FromStringDescriptor("max");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("flac_max", result.Id);
    }

    #endregion

    #region FromStringDescriptor - Unknown Descriptor

    [Fact]
    public void FromStringDescriptor_Unknown_ReturnsGenericQuality()
    {
        // Act - lines 230-235: unknown descriptor creates generic quality
        StreamingQuality? result = QualityMapper.FromStringDescriptor("custom_quality", "Qobuz");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("custom_quality", result.Id);
        Assert.Equal("Qobuz custom_quality", result.Name);
        Assert.Equal("Unknown", result.Format);
    }

    #endregion

    #region GetQualityDescription - Null Input

    [Fact]
    public void GetQualityDescription_Null_ReturnsUnknown()
    {
        // Act - line 244: null quality returns "Unknown Quality"
        string result = QualityMapper.GetQualityDescription(null!);

        // Assert
        Assert.Equal("Unknown Quality", result);
    }

    #endregion

    #region GetQualityDescription - Lossless HiRes

    [Fact]
    public void GetQualityDescription_LosslessHiRes_IncludesHiResTag()
    {
        // Arrange - HiRes lossless triggers lines 251-259
        StreamingQuality quality = StreamingQualityBuilder.CreateFlacHiRes();

        // Act
        string result = QualityMapper.GetQualityDescription(quality);

        // Assert - should contain "Hi-Res" tag
        Assert.Contains("Hi-Res", result);
        Assert.Contains("FLAC", result);
    }

    #endregion

    #region GetQualityDescription - Lossy Format

    [Fact]
    public void GetQualityDescription_LossyFormat_IncludesBitrate()
    {
        // Arrange - lossy format triggers lines 261-264
        StreamingQuality quality = StreamingQualityBuilder.CreateMp3320();

        // Act
        string result = QualityMapper.GetQualityDescription(quality);

        // Assert
        Assert.Contains("MP3", result);
        Assert.Contains("320kbps", result);
    }

    #endregion

    #region GetQualityDescription - Empty Quality

    [Fact]
    public void GetQualityDescription_EmptyQuality_ReturnsName()
    {
        // Arrange - quality with minimal info, no format
        StreamingQuality quality = new StreamingQuality { Id = "test", Name = "Test Quality" };

        // Act - line 267: fallback to name when no parts
        string result = QualityMapper.GetQualityDescription(quality);

        // Assert
        Assert.Equal("Test Quality", result);
    }

    #endregion

    #region QualityPreferenceMap - IsAcceptable

    [Fact]
    public void QualityPreferenceMap_IsAcceptable_WithinRange_ReturnsTrue()
    {
        // Arrange - line 285-288: IsAcceptable checks tier bounds
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless);

        // Act
        bool result = map.IsAcceptable(StreamingQualityTier.High);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void QualityPreferenceMap_IsAcceptable_BelowMin_ReturnsFalse()
    {
        // Arrange
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless, allowLower: false);

        // Act
        bool result = map.IsAcceptable(StreamingQualityTier.Low);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region QualityPreferenceMap - GetPreferenceScore

    [Fact]
    public void QualityPreferenceScore_NotAcceptable_ReturnsNegativeOne()
    {
        // Arrange - line 295: unacceptable tier returns -1
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless, allowLower: false);

        // Act
        int score = map.GetPreferenceScore(StreamingQualityTier.Low);

        // Assert
        Assert.Equal(-1, score);
    }

    [Fact]
    public void QualityPreferenceScore_ExactMatch_Returns100()
    {
        // Arrange - line 298: perfect match returns 100
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless);

        // Act
        int score = map.GetPreferenceScore(StreamingQualityTier.Lossless);

        // Assert
        Assert.Equal(100, score);
    }

    [Fact]
    public void QualityPreferenceScore_HigherAllowed_Returns90MinusDistance()
    {
        // Arrange - line 304-305: higher quality gets bonus
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless);

        // Act - HiRes is 1 tier above Lossless
        int score = map.GetPreferenceScore(StreamingQualityTier.HiRes);

        // Assert - 90 - 1 = 89
        Assert.Equal(89, score);
    }

    [Fact]
    public void QualityPreferenceScore_LowerAllowed_Returns80Minus10xDistance()
    {
        // Arrange - line 309-310: lower quality gets penalty
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless);

        // Act - High is 1 tier below Lossless
        int score = map.GetPreferenceScore(StreamingQualityTier.High);

        // Assert - 80 - (1 * 10) = 70
        Assert.Equal(70, score);
    }

    [Fact]
    public void QualityPreferenceScore_HigherNotAllowed_ReturnsNegativeOne()
    {
        // Arrange - higher quality not allowed: CreatePreferenceMap clamps MaxAcceptableTier
        // to the preferred tier, so HiRes is outside the [Min..Max] window and IsAcceptable
        // is false. Line 295: !IsAcceptable -> return -1 (the "not acceptable" sentinel).
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless, allowHigher: false);

        // Act
        int score = map.GetPreferenceScore(StreamingQualityTier.HiRes);

        // Assert - rejected by acceptance window
        Assert.Equal(-1, score);
    }

    [Fact]
    public void QualityPreferenceScore_LowerNotAllowed_ReturnsNegativeOne()
    {
        // Arrange - lower quality not allowed: MinAcceptableTier == Lossless, so High is below
        // the acceptance window and IsAcceptable returns false (line 295 -> return -1).
        QualityPreferenceMap map = QualityMapper.CreatePreferenceMap(StreamingQualityTier.Lossless, allowLower: false);

        // Act
        int score = map.GetPreferenceScore(StreamingQualityTier.High);

        // Assert - rejected by acceptance window
        Assert.Equal(-1, score);
    }

    #endregion

    #region StandardQualities - Verification

    [Fact]
    public void StandardQualities_Mp3Low_HasCorrectValues()
    {
        // Act - verify StandardQualities static fields (lines 19-25)
        StreamingQuality quality = QualityMapper.StandardQualities.Mp3Low;

        // Assert
        Assert.Equal("mp3_128", quality.Id);
        Assert.Equal("MP3 128kbps", quality.Name);
        Assert.Equal("MP3", quality.Format);
        Assert.Equal(128, quality.Bitrate);
    }

    [Fact]
    public void StandardQualities_Mp3Normal_HasCorrectValues()
    {
        // Act - lines 27-33
        StreamingQuality quality = QualityMapper.StandardQualities.Mp3Normal;

        // Assert
        Assert.Equal("mp3_256", quality.Id);
        Assert.Equal(256, quality.Bitrate);
    }

    [Fact]
    public void StandardQualities_Mp3High_HasCorrectValues()
    {
        // Act - lines 35-41
        StreamingQuality quality = QualityMapper.StandardQualities.Mp3High;

        // Assert
        Assert.Equal("mp3_320", quality.Id);
        Assert.Equal(320, quality.Bitrate);
    }

    [Fact]
    public void StandardQualities_FlacCD_HasCorrectValues()
    {
        // Act - lines 43-50
        StreamingQuality quality = QualityMapper.StandardQualities.FlacCD;

        // Assert
        Assert.Equal("flac_cd", quality.Id);
        Assert.Equal(44100, quality.SampleRate);
        Assert.Equal(16, quality.BitDepth);
    }

    [Fact]
    public void StandardQualities_FlacHiRes_HasCorrectValues()
    {
        // Act - lines 52-59
        StreamingQuality quality = QualityMapper.StandardQualities.FlacHiRes;

        // Assert
        Assert.Equal("flac_hires", quality.Id);
        Assert.Equal(96000, quality.SampleRate);
        Assert.Equal(24, quality.BitDepth);
    }

    [Fact]
    public void StandardQualities_FlacMax_HasCorrectValues()
    {
        // Act - lines 61-68
        StreamingQuality quality = QualityMapper.StandardQualities.FlacMax;

        // Assert
        Assert.Equal("flac_max", quality.Id);
        Assert.Equal(192000, quality.SampleRate);
        Assert.Equal(24, quality.BitDepth);
    }

    #endregion
}
