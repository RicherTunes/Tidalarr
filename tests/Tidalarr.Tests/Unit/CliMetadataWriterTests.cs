using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Models;
using Xunit;
using CliMetadataWriter = TidalCLI.CliMetadataWriter;

namespace Tidalarr.Tests.Unit;

public class CliMetadataWriterTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample_tone.m4a");

    [Fact]
    public async Task ApplyAlbumMetadataAsync_WritesCoreTagsAndCoverArt()
    {
        Assert.True(File.Exists(FixturePath), $"Fixture missing: {FixturePath}");

        var tempFile = Path.Combine(Path.GetTempPath(), $"metadata_{Guid.NewGuid():N}.m4a");
        try
        {
            File.Copy(FixturePath, tempFile, overwrite: true);

            var track = new TidalTrackInfo(
                Id: "track-1",
                Title: "Weird Fishes/Arpeggi",
                Artists: new List<string> { "Radiohead" },
                AlbumId: "album-1",
                AlbumTitle: "In Rainbows",
                TrackNumber: 5,
                Duration: 270,
                Quality: TidalQuality.Lossless,
                IsAvailable: true,
                ReleaseDate: new DateTime(2007, 10, 10));

            var album = new TidalAlbumInfo(
                Id: "album-1",
                Title: "In Rainbows",
                Artists: new List<string> { "Radiohead" },
                Tracks: new List<TidalTrackInfo> { track },
                AvailableQualities: new List<TidalQuality> { TidalQuality.Lossless },
                ReleaseDate: new DateTime(2007, 10, 10),
                CoverArtId: "1234567890abcdef",
                IsAvailable: true);

            var result = new DownloadResult
            {
                Success = true,
                TrackResults =
                {
                    new TrackDownloadResult
                    {
                        TrackId = track.Id,
                        Success = true,
                        FilePath = tempFile,
                        FileSize = new FileInfo(tempFile).Length
                    }
                }
            };

            static Task<byte[]?> CoverFetcher(string _) => Task.FromResult<byte[]?>(Convert.FromBase64String("/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD43ooor9wP6QP/2Q=="));

            await CliMetadataWriter.ApplyAlbumMetadataAsync(album, result, CoverFetcher);

            using var tagged = TagLib.File.Create(tempFile);
            Assert.Equal(track.Title, tagged.Tag.Title);
            Assert.Equal(album.Title, tagged.Tag.Album);
            Assert.Equal((uint)track.TrackNumber, tagged.Tag.Track);
            Assert.Equal<uint>((uint)album.ReleaseDate.Year, tagged.Tag.Year);
            Assert.Equal(new[] { "Radiohead" }, tagged.Tag.Performers);
            Assert.Equal(new[] { "Radiohead" }, tagged.Tag.AlbumArtists);
            Assert.Single(tagged.Tag.Pictures);
            Assert.True(tagged.Tag.Pictures[0].Data.Count > 0);
            Assert.Equal("image/jpeg", tagged.Tag.Pictures[0].MimeType);
        }
        finally
        {
            TryDelete(tempFile);
        }
    }

    [Fact]
    public void BuildCoverArtUrl_FormatsExpectedPattern()
    {
        var url = CliMetadataWriter.BuildCoverArtUrl("abcd-1234-efgh");
        Assert.Equal("https://resources.tidal.com/images/abcd/1234/efgh/1280x1280.jpg", url);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup only
        }
    }
}
