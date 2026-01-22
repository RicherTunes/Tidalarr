using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class DownloadConcurrencyCapTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 6, 6)]
    [InlineData(1, 8, 6)]
    [InlineData(2, 2, 2)]
    [InlineData(2, 3, 3)]
    [InlineData(2, 4, 3)]
    [InlineData(3, 2, 2)]
    [InlineData(3, 3, 2)]
    [InlineData(3, 8, 2)]
    public void GetEffectiveMaxConcurrentChunkDownloads_ClampsToCombinedCap(int tracks, int chunks, int expectedEffectiveChunks)
    {
        var settings = new TidalDownloadClientSettings
        {
            MaxConcurrentTrackDownloads = tracks,
            MaxConcurrentChunkDownloads = chunks
        };

        int effectiveChunks = settings.GetEffectiveMaxConcurrentChunkDownloads();

        Assert.Equal(expectedEffectiveChunks, effectiveChunks);
        Assert.InRange(tracks * effectiveChunks, 1, TidalDownloadClientSettings.MaxCombinedDownloadConcurrency);
    }
}

