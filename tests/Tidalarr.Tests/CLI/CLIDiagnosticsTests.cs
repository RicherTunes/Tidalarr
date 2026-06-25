using System.IO.Compression;
using System.Text.Json;

namespace Tidalarr.Tests.CLI;

public class CLIDiagnosticsTests
{
    private static string Temp => Path.GetTempPath();
    private static string RepoRoot
    {
        get
        {
            DirectoryInfo dir = new(Directory.GetCurrentDirectory());
            for (int i = 0; i < 7 && dir != null; i++, dir = dir.Parent!)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Tidalarr.sln")))
                {
                    return dir.FullName;
                }
            }
            return Directory.GetCurrentDirectory();
        }
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task SettingsValidate_Returns_CFG000_Json()
    {
        CliResult res = await RunCliAsync(
        [
            "settings-validate",
            $"ConfigPath={Temp}",
            "RedirectUrl=https://tidal.com/android/login/auth?code=test&state=state",
            $"DownloadPath={Temp}"
        ]);
        using JsonDocument doc = JsonDocument.Parse(res.Stdout);
        JsonElement root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("CFG000", root.GetProperty("value").GetProperty("id").GetString());
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task IndexerValidate_NoAuth_Returns_IX200_Json()
    {
        CliResult res = await RunCliAsync(
        [
            "indexer-validate",
            $"ConfigPath={Temp}",
            "RedirectUrl=https://tidal.com/android/login/auth?code=test&state=state",
            "TidalMarket=US"
        ]);
        using JsonDocument doc = JsonDocument.Parse(res.Stdout);
        JsonElement root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("IX200", root.GetProperty("error").GetProperty("metadata").GetProperty("id").GetString());
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task DownloadValidate_NoAuth_Returns_DL100_Json()
    {
        CliResult res = await RunCliAsync(
        [
            "download-validate",
            "TrackId=t1",
            "Quality=Lossless",
            $"DownloadPath={Temp}"
        ]);
        using JsonDocument doc = JsonDocument.Parse(res.Stdout);
        JsonElement root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        string? id = root.GetProperty("error").GetProperty("metadata").GetProperty("id").GetString();
        Assert.True(id is "DL100" or "DL001", $"Unexpected id: {id}");
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task DownloadValidate_Invalid_Quality_Returns_DLVAL()
    {
        CliResult res = await RunCliAsync(
        [
            "download-validate",
            "TrackId=t1",
            "Quality=INVALID",
            $"DownloadPath={Temp}"
        ]);
        using JsonDocument doc = JsonDocument.Parse(res.Stdout);
        JsonElement err = doc.RootElement.GetProperty("error").GetProperty("metadata");
        Assert.Equal("DLVAL", err.GetProperty("id").GetString());
        Assert.Equal("Quality", err.GetProperty("field").GetString());
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task SettingsValidate_UnknownKey_Returns_CFGVAL()
    {
        CliResult res = await RunCliAsync(
        [
            "settings-validate",
            $"ConfigPath={Temp}",
            "RedirectUrl=https://tidal.com/android/login/auth?code=test&state=state",
            $"DownloadPath={Temp}",
            "UnknownKey=foo"
        ]);
        using JsonDocument doc = JsonDocument.Parse(res.Stdout);
        JsonElement err = doc.RootElement.GetProperty("error").GetProperty("metadata");
        Assert.Equal("CFGVAL", err.GetProperty("id").GetString());
        Assert.Equal("Unknown", err.GetProperty("field").GetString());
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public void Package_Dependency_Closure_Has_No_Host_Assemblies()
    {
        // Invoke packaging to produce a zip, then assert closure excludes host assemblies.
        System.Diagnostics.ProcessStartInfo ps = new()
        {
            FileName = "pwsh",
            Arguments = "-NoLogo -NoProfile -Command \"./build.ps1 -Package -Configuration Release\"",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using System.Diagnostics.Process proc = System.Diagnostics.Process.Start(ps)!;
        _ = proc.WaitForExit(300_000);

        string packagesDir = Path.Combine(RepoRoot, "src", "Tidalarr", "artifacts", "packages");
        Assert.True(Directory.Exists(packagesDir), $"Packages directory not found: {packagesDir}");
        string zip = Directory.EnumerateFiles(packagesDir, "*.zip").OrderByDescending(File.GetCreationTimeUtc).First();
        Assert.True(File.Exists(zip), "Package zip not found.");

        using ZipArchive archive = ZipFile.OpenRead(zip);
        string[] dlls = [.. archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Select(e => e.Name)];
        // Allowed: plugin + common runtime
        string[] allowed = ["Lidarr.Plugin.Tidalarr.dll", "Lidarr.Plugin.Common.dll"];

        // No Lidarr.* other than the allowed set
        string[] disallowed = [.. dlls.Where(n => n.StartsWith("Lidarr.", StringComparison.OrdinalIgnoreCase) && !allowed.Contains(n))];
        Assert.True(disallowed.Length == 0, $"Disallowed host assemblies found: {string.Join(", ", disallowed)}");
    }

    private readonly record struct CliResult(int ExitCode, string Stdout, string Stderr);

    private static async Task<CliResult> RunCliAsync(string[] args)
    {
        // Build CLI to ensure consistent output path
        System.Diagnostics.ProcessStartInfo buildInfo = new()
        {
            FileName = "dotnet",
            Arguments = "build TidalCLI/TidalCLI.csproj -c Release -v minimal",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using (System.Diagnostics.Process build = System.Diagnostics.Process.Start(buildInfo)!)
        {
            await build.WaitForExitAsync();
            if (build.ExitCode != 0)
            {
                return new CliResult(-1, string.Empty, "dotnet build failed");
            }
        }
        string cliDll = Path.Combine(RepoRoot, "TidalCLI", "bin", "Release", "net8.0", "TidalCLI.dll");

        // Ensure host shim assemblies are present for settings types that reference NzbDrone.*
        string hostOutput = Path.Combine(RepoRoot, "ext", "Lidarr", "_output", "net8.0");
        if (Directory.Exists(hostOutput))
        {
            foreach (string? dll in new[] { "Lidarr.Core.dll", "Lidarr.Common.dll" })
            {
                string src = Path.Combine(hostOutput, dll);
                string dst = Path.Combine(Path.GetDirectoryName(cliDll)!, dll);
                if (File.Exists(src) && !File.Exists(dst))
                {
                    File.Copy(src, dst, overwrite: false);
                }
            }
        }

        System.Diagnostics.ProcessStartInfo psi = new()
        {
            FileName = "dotnet",
            Arguments = $"\"{cliDll}\" {string.Join(' ', args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a))}",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using System.Diagnostics.Process proc = System.Diagnostics.Process.Start(psi)!;
        string stdout = await proc.StandardOutput.ReadToEndAsync();
        string stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return new CliResult(proc.ExitCode, stdout.Trim(), stderr.Trim());
    }
}

