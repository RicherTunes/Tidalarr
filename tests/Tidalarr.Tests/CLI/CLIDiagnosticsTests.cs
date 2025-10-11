using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Tidalarr.Tests.CLI;

public class CLIDiagnosticsTests
{
    private static string Temp => Path.GetTempPath();
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 7 && dir != null; i++, dir = dir.Parent!)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Tidalarr.sln"))) return dir.FullName;
            }
            return Directory.GetCurrentDirectory();
        }
    }

    [Tidalarr.Tests.Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task SettingsValidate_Returns_CFG000_Json()
    {
        var res = await RunCliAsync(new[]
        {
            "settings-validate",
            $"ConfigPath={Temp}",
            "RedirectUrl=https://tidal.com/android/login/auth?code=test&state=state",
            $"DownloadPath={Temp}"
        });
        using var doc = JsonDocument.Parse(res.Stdout);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("CFG000", root.GetProperty("value").GetProperty("id").GetString());
    }

    [Tidalarr.Tests.Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task IndexerValidate_NoAuth_Returns_IX200_Json()
    {
        var res = await RunCliAsync(new[]
        {
            "indexer-validate",
            $"ConfigPath={Temp}",
            "RedirectUrl=https://tidal.com/android/login/auth?code=test&state=state",
            "TidalMarket=US"
        });
        using var doc = JsonDocument.Parse(res.Stdout);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("IX200", root.GetProperty("error").GetProperty("metadata").GetProperty("id").GetString());
    }

    [Tidalarr.Tests.Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task DownloadValidate_NoAuth_Returns_DL100_Json()
    {
        var res = await RunCliAsync(new[]
        {
            "download-validate",
            "TrackId=t1",
            "Quality=Lossless",
            $"DownloadPath={Temp}"
        });
        using var doc = JsonDocument.Parse(res.Stdout);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        var id = root.GetProperty("error").GetProperty("metadata").GetProperty("id").GetString();
        Assert.True(id == "DL100" || id == "DL001", $"Unexpected id: {id}");
    }

    [Tidalarr.Tests.Utils.CliFact]
    [Trait("scope", "cli")]
    public void Package_Dependency_Closure_Has_No_Host_Assemblies()
    {
        // Invoke packaging to produce a zip, then assert closure excludes host assemblies.
        var ps = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = "-NoLogo -NoProfile -Command \"./build.ps1 -Package -Configuration Release\"",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = System.Diagnostics.Process.Start(ps)!;
        proc.WaitForExit(300_000);

        var packagesDir = Path.Combine(RepoRoot, "src", "Tidalarr", "artifacts", "packages");
        Assert.True(Directory.Exists(packagesDir), $"Packages directory not found: {packagesDir}");
        var zip = Directory.EnumerateFiles(packagesDir, "*.zip").OrderByDescending(File.GetCreationTimeUtc).First();
        Assert.True(File.Exists(zip), "Package zip not found.");

        using var archive = ZipFile.OpenRead(zip);
        var dlls = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(e => e.Name).ToArray();
        // Allowed: plugin + common runtime
        string[] allowed = new[] { "Lidarr.Plugin.Tidalarr.dll", "Lidarr.Plugin.Common.dll" };

        // No Lidarr.* other than the allowed set
        var disallowed = dlls.Where(n => n.StartsWith("Lidarr.", StringComparison.OrdinalIgnoreCase) && !allowed.Contains(n)).ToArray();
        Assert.True(disallowed.Length == 0, $"Disallowed host assemblies found: {string.Join(", ", disallowed)}");
    }

    private readonly record struct CliResult(int ExitCode, string Stdout, string Stderr);

    private static async Task<CliResult> RunCliAsync(string[] args)
    {
        // Build CLI to ensure consistent output path
        var buildInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build TidalCLI/TidalCLI.csproj -c Release -v minimal",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using (var build = System.Diagnostics.Process.Start(buildInfo)!)
        {
            await build.WaitForExitAsync();
            if (build.ExitCode != 0)
            {
                return new CliResult(-1, string.Empty, "dotnet build failed");
            }
        }
        var cliDll = Path.Combine(RepoRoot, "TidalCLI", "bin", "Release", "net9.0", "TidalCLI.dll");

        // Ensure host shim assemblies are present for settings types that reference NzbDrone.*
        var hostOutput = Path.Combine(RepoRoot, "ext", "Lidarr", "_output", "net6.0");
        if (Directory.Exists(hostOutput))
        {
            foreach (var dll in new[] { "Lidarr.Core.dll", "Lidarr.Common.dll" })
            {
                var src = Path.Combine(hostOutput, dll);
                var dst = Path.Combine(Path.GetDirectoryName(cliDll)!, dll);
                if (File.Exists(src) && !File.Exists(dst))
                {
                    File.Copy(src, dst, overwrite: false);
                }
            }
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{cliDll}\" {string.Join(' ', args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a))}",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return new CliResult(proc.ExitCode, stdout.Trim(), stderr.Trim());
    }
}


