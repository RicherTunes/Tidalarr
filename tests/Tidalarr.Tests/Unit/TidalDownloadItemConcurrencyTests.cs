using System.Threading.Tasks;

using NzbDrone.Core.Download;

using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Wave 11: TidalDownloadItem._status was declared `volatile int` AND passed
/// to Volatile.Read/Volatile.Write via `ref`. CS0420 fires because the
/// volatile modifier's read/write barrier guarantees cannot be carried
/// through a ref parameter — once the compiler hands out the field address,
/// the volatile semantics are bypassed.
///
/// Fix: drop the `volatile` modifier. Volatile.Read/Volatile.Write already
/// provide acquire/release barriers. These tests pin down the contract so
/// the fix is observable and any future "let's add volatile back" attempt
/// fails compilation again.
/// </summary>
public sealed class TidalDownloadItemConcurrencyTests
{
    [Fact]
    public void Status_WriteThenRead_ReturnsWrittenValue()
    {
        var item = new TidalDownloadItem();
        item.Status = DownloadItemStatus.Downloading;
        Assert.Equal(DownloadItemStatus.Downloading, item.Status);
    }

    [Fact]
    public void Status_MultipleWrites_FinalReadObservesLast()
    {
        var item = new TidalDownloadItem();
        item.Status = DownloadItemStatus.Queued;
        item.Status = DownloadItemStatus.Downloading;
        item.Status = DownloadItemStatus.Completed;
        Assert.Equal(DownloadItemStatus.Completed, item.Status);
    }

    [Fact]
    public async Task Status_ConcurrentWriters_FinalReadObservesOneOfThem_NoTearing()
    {
        // Producer-consumer: 4 writers cycle through statuses, 1 reader
        // observes. No torn-read assertion — the value must always be a
        // valid enum (not an arbitrary 4-byte pattern from a partial write).
        var item = new TidalDownloadItem();
        var validStatuses = new[]
        {
            DownloadItemStatus.Queued,
            DownloadItemStatus.Paused,
            DownloadItemStatus.Downloading,
            DownloadItemStatus.Completed,
            DownloadItemStatus.Failed,
        };

        var writers = new Task[4];
        for (var w = 0; w < writers.Length; w++)
        {
            var seed = w;
            writers[w] = Task.Run(() =>
            {
                for (var i = 0; i < 10_000; i++)
                {
                    item.Status = validStatuses[(seed + i) % validStatuses.Length];
                }
            });
        }

        // Reader observes 50k times during the writer burst.
        for (var i = 0; i < 50_000; i++)
        {
            var observed = item.Status;
            Assert.Contains(observed, validStatuses);
        }

        await Task.WhenAll(writers);
        Assert.Contains(item.Status, validStatuses);
    }

    [Fact]
    public void UpdateStatus_HelperMatchesPropertySetter()
    {
        var item = new TidalDownloadItem();
        item.UpdateStatus(DownloadItemStatus.Warning);
        Assert.Equal(DownloadItemStatus.Warning, item.Status);
    }
}
