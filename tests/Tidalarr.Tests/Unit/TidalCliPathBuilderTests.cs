using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Tidalarr.Core.Models;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class TidalCliPathBuilderTests
{
    [Fact]
    public void BuildAlbumOutputDirectory_ValidAlbum_ReturnsArtistAlbumStructure()
    {
        var root = Path.Combine(Path.GetTempPath(), "tidalarr-test-root");
        var album = new TidalAlbumInfo(
            Id: "61799588",
            Title: "In Rainbows",
            Artists: new List<string> { "Radiohead" },
            Tracks: new List<TidalTrackInfo>(),
            AvailableQualities: new List<TidalQuality>(),
            ReleaseDate: new DateTime(2007, 10, 10),
            CoverArtId: "cover-art",
            IsAvailable: true);

        var method = typeof(TidalCLI.Program).GetMethod("BuildAlbumOutputDirectory", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var output = (string)method!.Invoke(null, new object[] { root, album })!;
        var expected = Path.Combine(root, "Radiohead", "In Rainbows (2007)");

        Assert.Equal(expected, output);
    }
}


