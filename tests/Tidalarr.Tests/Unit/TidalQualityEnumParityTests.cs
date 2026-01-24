using System;
using System.Collections.Generic;
using System.Linq;
using Tidalarr.HostBridge.Settings;
using Tidalarr.Core.Models;

namespace Tidalarr.Tests.Unit;

public class TidalQualityEnumParityTests
{
    [Fact]
    public void Host_And_Core_Quality_Enums_Should_Match_Names_And_Values()
    {
        var core = Enum.GetValues<TidalQuality>()
            .Select(v => (Name: v.ToString(), Value: Convert.ToInt32(v)))
            .ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);

        var host = Enum.GetValues<TidalQualityHost>()
            .Select(v => (Name: v.ToString(), Value: Convert.ToInt32(v)))
            .ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);

        Assert.Equal(core.Count, host.Count);

        foreach (KeyValuePair<string, int> coreEntry in core)
        {
            Assert.True(host.TryGetValue(coreEntry.Key, out var hostValue), $"Missing host enum value: {coreEntry.Key}");
            Assert.Equal(coreEntry.Value, hostValue);
        }
    }
}

