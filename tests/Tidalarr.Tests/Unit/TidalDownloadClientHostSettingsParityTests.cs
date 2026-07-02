using Tidalarr.HostBridge.Settings;

namespace Tidalarr.Tests.Unit;

public class TidalDownloadClientHostSettingsParityTests
{
    [Fact]
    public void ToCore_Should_Map_All_UserVisible_Fields()
    {
        TidalDownloadClientHostSettings host = new()
        {
            PreferredQuality = TidalQualityHost.HiRes,
            DownloadPath = "C:/out",
            ExtractFlac = false,
            SaveSyncedLyrics = false,
            UseLRCLIB = true,
            DownloadDelay = 123,
            MaxConcurrentTrackDownloads = 3,
            MaxConcurrentChunkDownloads = 2
        };

        Tidalarr.Integration.TidalDownloadClientSettings core = host.ToCore();

        Assert.Equal(Core.Models.TidalQuality.HiRes, core.PreferredQuality);
        Assert.Equal(host.DownloadPath, core.DownloadPath);
        Assert.Equal(host.ExtractFlac, core.ExtractFlac);
        Assert.Equal(host.SaveSyncedLyrics, core.SaveSyncedLyrics);
        Assert.Equal(host.UseLRCLIB, core.UseLRCLIB);
        Assert.Equal(host.DownloadDelay, core.DownloadDelay);
        Assert.Equal(host.MaxConcurrentTrackDownloads, core.MaxConcurrentTrackDownloads);
        Assert.Equal(host.MaxConcurrentChunkDownloads, core.MaxConcurrentChunkDownloads);
    }
}
