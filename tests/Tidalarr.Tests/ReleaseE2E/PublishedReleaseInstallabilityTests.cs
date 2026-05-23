using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Tidalarr.Tests.ReleaseE2E;

/// <summary>
/// Simulates Lidarr's <c>PluginService.GetRemotePlugin</c> filter against the LIVE
/// GitHub releases of this repo, then verifies the most-recent installable release's
/// zip satisfies the packaging policy.
///
/// Mirrors the same test added to brainarr/qobuzarr — uniform across the four-plugin
/// family. See brainarr's PublishedReleaseInstallabilityTests for full rationale.
///
/// Opt-in via [Trait("Category", "ReleaseE2E")] — skipped in the default test sweep.
/// </summary>
public class PublishedReleaseInstallabilityTests
{
    private const string Owner = "RicherTunes";
    private const string Repo = "Tidalarr";
    private const string Framework = "net8.0";

    private static readonly string[] RequiredFiles =
    {
        "Lidarr.Plugin.Tidalarr.dll",
        "plugin.json",
    };

    private static readonly string[] ForbiddenAssemblies =
    {
        "FluentValidation.dll",
        "NLog.dll",
        "System.Text.Json.dll",
        "Newtonsoft.Json.dll",
        "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
        "Microsoft.Extensions.Logging.Abstractions.dll",
        "Microsoft.Extensions.Caching.Abstractions.dll",
        "Microsoft.Extensions.Caching.Memory.dll",
        "Microsoft.Extensions.Options.dll",
        "Microsoft.Extensions.Primitives.dll",
        "Lidarr.Core.dll",
        "Lidarr.Common.dll",
        "Lidarr.Http.dll",
        "Lidarr.Api.V1.dll",
        "NzbDrone.Core.dll",
        "NzbDrone.Common.dll",
        "Lidarr.Plugin.Abstractions.dll",
        "Lidarr.Plugin.Common.dll",
    };

    [SkippableFact]
    [Trait("Category", "ReleaseE2E")]
    public async Task LatestPublishedRelease_PassesLidarrInstallFilter()
    {
        using HttpClient http = CreateClient();
        var releases = await TryGetReleasesAsync(http);
        Skip.If(releases is null, "GitHub releases unavailable");

        var installable = releases!.Value.EnumerateArray()
            .Where(r => !r.GetProperty("draft").GetBoolean())
            .Where(r => IsDefaultTree(r.GetProperty("target_commitish").GetString()))
            .Where(r => r.GetProperty("assets").EnumerateArray()
                .Any(a => a.GetProperty("name").GetString()?.Contains($"{Framework}.zip", StringComparison.OrdinalIgnoreCase) == true))
            .ToList();

        Assert.True(installable.Count > 0,
            $"No release passes Lidarr's PluginService filter. UI Install on https://github.com/{Owner}/{Repo} would silently fail.");
    }

    [SkippableFact]
    [Trait("Category", "ReleaseE2E")]
    public async Task LatestPublishedRelease_ZipContents_Match_PackagingPolicy()
    {
        using HttpClient http = CreateClient();
        var releases = await TryGetReleasesAsync(http);
        Skip.If(releases is null, "GitHub releases unavailable");

        var topRelease = releases!.Value.EnumerateArray()
            .Where(r => !r.GetProperty("draft").GetBoolean())
            .Where(r => IsDefaultTree(r.GetProperty("target_commitish").GetString()))
            .FirstOrDefault(r => r.GetProperty("assets").EnumerateArray()
                .Any(a => a.GetProperty("name").GetString()?.Contains($"{Framework}.zip", StringComparison.OrdinalIgnoreCase) == true));

        Skip.If(topRelease.ValueKind == JsonValueKind.Undefined, "No installable release found");

        var asset = topRelease.GetProperty("assets").EnumerateArray()
            .First(a => a.GetProperty("name").GetString()?.Contains($"{Framework}.zip", StringComparison.OrdinalIgnoreCase) == true);

        var downloadUrl = asset.GetProperty("browser_download_url").GetString()!;
        await using Stream zipStream = await http.GetStreamAsync(downloadUrl);
        using var ms = new MemoryStream();
        await zipStream.CopyToAsync(ms);
        ms.Position = 0;
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        var fileNames = archive.Entries
            .Select(e => Path.GetFileName(e.FullName))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assetName = asset.GetProperty("name").GetString();
        foreach (var required in RequiredFiles)
        {
            Assert.True(fileNames.Contains(required),
                $"Published release '{assetName}' is missing required file '{required}'. Contents: {string.Join(", ", fileNames)}");
        }

        foreach (var forbidden in ForbiddenAssemblies)
        {
            Assert.False(fileNames.Contains(forbidden),
                $"Published release '{assetName}' ships FORBIDDEN '{forbidden}'. Contents: {string.Join(", ", fileNames)}");
        }

        var mainDllEntry = archive.Entries.FirstOrDefault(e =>
            Path.GetFileName(e.FullName).Equals("Lidarr.Plugin.Tidalarr.dll", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(mainDllEntry);
        Assert.True(mainDllEntry!.Length >= 2_000_000,
            $"Lidarr.Plugin.Tidalarr.dll is only {mainDllEntry.Length} bytes — expected ≥2MB (merged DLL). " +
            $"Sub-threshold means ILRepack didn't run.");
    }

    private static bool IsDefaultTree(string? target) =>
        string.Equals(target, "main", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(target, "master", StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{Repo}-tests/1.0");
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    private static async Task<JsonElement?> TryGetReleasesAsync(HttpClient http)
    {
        try
        {
            using var response = await http.GetAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page=30");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
