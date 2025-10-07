using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using TagLib;
using Tidalarr.Core.Models;

namespace TidalCLI;

internal static class CliMetadataWriter
{
    private static readonly HttpClient CoverArtClient = CreateCoverArtClient();

    public static async Task ApplyAlbumMetadataAsync(TidalAlbumInfo? album, DownloadResult downloadResult, Func<string, Task<byte[]?>>? coverArtFetcher = null)
    {
        if (album == null || album.Tracks == null || album.Tracks.Count == 0)
        {
            return;
        }

        if (downloadResult == null || downloadResult.TrackResults == null || downloadResult.TrackResults.Count == 0)
        {
            return;
        }

        var trackLookup = album.Tracks.ToDictionary(t => t.Id, StringComparer.Ordinal);
        coverArtFetcher ??= DownloadCoverArtAsync;

        byte[]? coverArtBytes = null;
        if (!string.IsNullOrWhiteSpace(album.CoverArtId))
        {
            coverArtBytes = await SafeFetchCoverAsync(coverArtFetcher, album.CoverArtId).ConfigureAwait(false);
        }

        foreach (var trackResult in downloadResult.TrackResults)
        {
            if (!trackResult.Success || string.IsNullOrWhiteSpace(trackResult.FilePath))
            {
                continue;
            }

            if (!trackLookup.TryGetValue(trackResult.TrackId, out var trackInfo))
            {
                Console.WriteLine($"⚠️ Skipping metadata tagging for {Path.GetFileName(trackResult.FilePath)}: track metadata not found");
                continue;
            }

            try
            {
                ApplyTags(trackResult.FilePath, album, trackInfo, coverArtBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Metadata tagging failed for {Path.GetFileName(trackResult.FilePath)}: {ex.Message}");
            }
        }
    }

    internal static string BuildCoverArtUrl(string coverArtId, int size = 1280)
    {
        if (string.IsNullOrWhiteSpace(coverArtId))
        {
            return string.Empty;
        }

        var normalized = coverArtId.Replace("-", "/", StringComparison.Ordinal);
        return $"https://resources.tidal.com/images/{normalized}/{size}x{size}.jpg";
    }

    internal static async Task<byte[]?> DownloadCoverArtAsync(string coverArtId)
    {
        var url = BuildCoverArtUrl(coverArtId);
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        try
        {
            using var response = await CoverArtClient.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> SafeFetchCoverAsync(Func<string, Task<byte[]?>> fetcher, string coverArtId)
    {
        try
        {
            return await fetcher(coverArtId).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyTags(string filePath, TidalAlbumInfo album, TidalTrackInfo track, byte[]? coverArtBytes)
    {
        using var tagFile = TagLib.File.Create(filePath);

        tagFile.Tag.Title = track.Title;
        tagFile.Tag.Album = album.Title;
        tagFile.Tag.Performers = track.Artists?.ToArray() ?? Array.Empty<string>();
        tagFile.Tag.AlbumArtists = album.Artists?.ToArray() ?? Array.Empty<string>();
        if (track.TrackNumber > 0)
        {
            tagFile.Tag.Track = (uint)track.TrackNumber;
        }

        if (album.ReleaseDate != default)
        {
            tagFile.Tag.Year = (uint)album.ReleaseDate.Year;
        }

        if (coverArtBytes?.Length > 0)
        {
            var picture = new Picture(new ByteVector(coverArtBytes))
            {
                MimeType = "image/jpeg",
                Type = PictureType.FrontCover,
                Description = "Cover"
            };

            if (tagFile.GetTag(TagTypes.Apple, create: true) is TagLib.Mpeg4.AppleTag appleTag)
            {
                appleTag.Pictures = new IPicture[] { picture };
            }

            tagFile.Tag.Pictures = new IPicture[] { picture };
        }

        tagFile.Save();

        if (tagFile is TagLib.Mpeg4.File mpeg4Save)
        {
            mpeg4Save.Save();
        }
    }

    private static HttpClient CreateCoverArtClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        var client = new HttpClient(handler, disposeHandler: true);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Tidalarr CLI Metadata/1.0");
        return client;
    }
}

