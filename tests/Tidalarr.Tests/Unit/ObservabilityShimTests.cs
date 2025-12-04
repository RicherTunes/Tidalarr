using Microsoft.Extensions.Logging.Abstractions;
using Tidalarr.Infrastructure.Observability;

namespace Tidalarr.Tests.Unit;

public class ObservabilityShimTests
{
    [Fact]
    public void StartApi_NoFlag_NoThrow_Noops()
    {
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", null);
            using IDisposable d = ObservabilityShim.StartApi(NullLogger.Instance, "tidal", "search");
            // If we got here, it didn't throw, which is expected when disabled
            Assert.NotNull(d);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }

    [Fact]
    public void CompleteApi_NoHelpers_NoThrow()
    {
        string? prev = Environment.GetEnvironmentVariable("TIDALARR_OBS");
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", "1");
            ObservabilityShim.CompleteApi(NullLogger.Instance, "tidal", "search", 200, true, TimeSpan.FromMilliseconds(1));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_OBS", prev);
        }
    }
}

