using System;
using System.Linq;
using System.Reflection;
using Lidarr.Plugin.Common.Utilities;
using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Common's SettingsSnapshot copies the live settings before the background download reads them. The previous
/// hand-written initializer dropped SaveSyncedLyrics + UseLRCLIB, so the background download ignored the
/// user's lyric settings. These tests pin the full copy contract and guard against reintroducing a local
/// per-plugin snapshot fork.
/// </summary>
public sealed class TidalLidarrDownloadClientSnapshotTests
{
    [Fact]
    public void DownloadClient_DoesNotDeclareLocalSnapshotSettingsFork()
    {
        var localSnapshot = typeof(TidalLidarrDownloadClient).GetMethod(
            "SnapshotSettings",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        Assert.Null(localSnapshot);
    }

    [Fact]
    public void CommonSettingsSnapshot_CopiesEveryReadWriteProperty()
    {
        TidalLidarrDownloadClientSettings live = new();

        var props = typeof(TidalLidarrDownloadClientSettings)
            .GetProperties()
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToList();

        // Set every property to a non-default sentinel so a dropped copy is detectable.
        foreach (var p in props)
        {
            object value = p.PropertyType switch
            {
                var t when t == typeof(string) => "snapshot-sentinel",
                var t when t == typeof(int) => 7,
                var t when t == typeof(int?) => 7,
                var t when t == typeof(bool) => true,
                var t when t.IsEnum => Enum.GetValues(t).Cast<object>().Last(),
                _ => throw new InvalidOperationException($"Add a sentinel for {p.PropertyType} ({p.Name}).")
            };
            p.SetValue(live, value);
        }

        var snapshot = SettingsSnapshot.Copy(live);

        foreach (var p in props)
        {
            Assert.Equal(p.GetValue(live), p.GetValue(snapshot));
        }
    }

    [Fact]
    public void CommonSettingsSnapshot_CopiesLyricsFields_PreviouslyDropped()
    {
        TidalLidarrDownloadClientSettings live = new() { SaveSyncedLyrics = true, UseLRCLIB = false };

        var snapshot = SettingsSnapshot.Copy(live);

        Assert.True(snapshot.SaveSyncedLyrics);
        Assert.False(snapshot.UseLRCLIB);
    }
}
