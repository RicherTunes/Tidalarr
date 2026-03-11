using System.Text.Json;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// TDD tests to lock DTO deserialization contracts for TidalPlaybackInfoDto.
/// These tests prevent regressions from type mismatches discovered in E2E testing.
///
/// Historical context:
/// - trackId was originally string? but Tidal API returns numeric (e.g., 2178486)
/// - replayGain/peakAmplitude were originally int? but API returns floats (e.g., -5.3)
/// </summary>
public class TidalPlaybackInfoDtoDeserializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void TidalPlaybackInfoDto_NumericTrackId_DeserializesCorrectly()
    {
        // Arrange - Real-world JSON with numeric trackId (not quoted string)
        const string json = /*lang=json,strict*/ """
        {
            "trackId": 2178486,
            "assetPresentation": "FULL",
            "audioQuality": "LOSSLESS",
            "manifest": "base64data=="
        }
        """;

        // Act
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(2178486L, dto.trackId);
    }

    [Fact]
    public void TidalPlaybackInfoDto_ReplayGainAsFloat_DeserializesCorrectly()
    {
        // Arrange - Real-world JSON with float replayGain values
        const string json = /*lang=json,strict*/ """
        {
            "trackId": 123456,
            "albumReplayGain": -5.3,
            "trackReplayGain": -4.7
        }
        """;

        // Act
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(-5.3, dto.albumReplayGain!.Value, 3);
        Assert.Equal(-4.7, dto.trackReplayGain!.Value, 3);
    }

    [Fact]
    public void TidalPlaybackInfoDto_PeakAmplitudeAsFloat_DeserializesCorrectly()
    {
        // Arrange - Real-world JSON with float peakAmplitude values
        const string json = /*lang=json,strict*/ """
        {
            "trackId": 789012,
            "albumPeakAmplitude": 0.987654,
            "trackPeakAmplitude": 0.876543
        }
        """;

        // Act
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(0.987654, dto.albumPeakAmplitude!.Value, 6);
        Assert.Equal(0.876543, dto.trackPeakAmplitude!.Value, 6);
    }

    [Fact]
    public void TidalPlaybackInfoDto_NullableFieldsAsNull_DeserializesCorrectly()
    {
        // Arrange - Minimal JSON with only required field
        const string json = /*lang=json,strict*/ """
        {
            "trackId": 111222
        }
        """;

        // Act
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(111222L, dto.trackId);
        Assert.Null(dto.albumReplayGain);
        Assert.Null(dto.trackReplayGain);
        Assert.Null(dto.albumPeakAmplitude);
        Assert.Null(dto.trackPeakAmplitude);
        Assert.Null(dto.bitDepth);
        Assert.Null(dto.sampleRate);
    }

    [Fact]
    public void TidalPlaybackInfoDto_BitDepthAndSampleRate_DeserializesCorrectly()
    {
        // Arrange - JSON with audio technical specs
        const string json = /*lang=json,strict*/ """
        {
            "trackId": 333444,
            "bitDepth": 24,
            "sampleRate": 96000
        }
        """;

        // Act
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(24, dto.bitDepth);
        Assert.Equal(96000, dto.sampleRate);
    }

    [Fact]
    public void TidalPlaybackInfoDto_FullResponse_DeserializesCorrectly()
    {
        // Arrange - Realistic full playback info response
        const string json = /*lang=json,strict*/ """
        {
            "trackId": 2178486,
            "assetPresentation": "FULL",
            "audioQuality": "HI_RES_LOSSLESS",
            "audioMode": "STEREO",
            "manifestMimeType": "application/dash+xml",
            "manifest": "PD94bWwgdmVyc2lvbj0iMS4wIj8+",
            "encryptionType": "NONE",
            "albumPeakAmplitude": 0.999969,
            "albumReplayGain": -8.24,
            "trackPeakAmplitude": 0.764282,
            "trackReplayGain": -6.53,
            "bitDepth": 24,
            "sampleRate": 96000
        }
        """;

        // Act
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(2178486L, dto.trackId);
        Assert.Equal("FULL", dto.assetPresentation);
        Assert.Equal("HI_RES_LOSSLESS", dto.audioQuality);
        Assert.Equal("STEREO", dto.audioMode);
        Assert.Equal("application/dash+xml", dto.manifestMimeType);
        Assert.Equal("PD94bWwgdmVyc2lvbj0iMS4wIj8+", dto.manifest);
        Assert.Equal("NONE", dto.encryptionType);
        Assert.Equal(0.999969, dto.albumPeakAmplitude!.Value, 6);
        Assert.Equal(-8.24, dto.albumReplayGain!.Value, 2);
        Assert.Equal(0.764282, dto.trackPeakAmplitude!.Value, 6);
        Assert.Equal(-6.53, dto.trackReplayGain!.Value, 2);
        Assert.Equal(24, dto.bitDepth);
        Assert.Equal(96000, dto.sampleRate);
    }

    [Fact]
    public void TidalPlaybackInfoDto_ReplayGainAsInteger_DeserializesCorrectly()
    {
        // Arrange - Edge case: API might return integer where double expected
        const string json = /*lang=json,strict*/ """
        {
            "trackId": 555666,
            "albumReplayGain": -5,
            "trackReplayGain": 0
        }
        """;

        // Act
        TidalPlaybackInfoDto? dto = JsonSerializer.Deserialize<TidalPlaybackInfoDto>(json, JsonOptions);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(-5.0, dto.albumReplayGain);
        Assert.Equal(0.0, dto.trackReplayGain);
    }
}
