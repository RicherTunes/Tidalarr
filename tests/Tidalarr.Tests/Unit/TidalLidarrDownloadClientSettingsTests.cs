using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Guards that the host-facing download settings propagate the lyrics toggles into the internal
/// TidalDownloadClientSettings the download pipeline reads. These were previously defined only on
/// the internal class (invisible to the user) and dropped by ToTidalSettings(), so the lyrics
/// feature ran with hardcoded defaults regardless of user intent.
/// </summary>
public class TidalLidarrDownloadClientSettingsTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ToTidalSettings_propagates_lyrics_toggles(bool saveSyncedLyrics, bool useLrclib)
    {
        var host = new TidalLidarrDownloadClientSettings
        {
            DownloadPath = "/music",
            SaveSyncedLyrics = saveSyncedLyrics,
            UseLRCLIB = useLrclib,
        };

        var mapped = host.ToTidalSettings();

        Assert.Equal(saveSyncedLyrics, mapped.SaveSyncedLyrics);
        Assert.Equal(useLrclib, mapped.UseLRCLIB);
    }
}
