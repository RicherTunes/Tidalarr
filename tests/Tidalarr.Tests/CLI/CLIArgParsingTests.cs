namespace Tidalarr.Tests.CLI;

public class CLIArgParsingTests
{
    private static string Temp => Path.GetTempPath();
    private static string RepoRoot
    {
        get
        {
            DirectoryInfo dir = new(Directory.GetCurrentDirectory());
            for (int i = 0; i < 7 && dir != null; i++, dir = dir.Parent!)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Tidalarr.sln"))) return dir.FullName;
            }
            return Directory.GetCurrentDirectory();
        }
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task Search_With_Query_Key_Works_Or_Shows_NotAuthenticated()
    {
        CliResult res = await RunCliAsync(["search", "Query=Bohemian Rhapsody Queen"]);
        Assert.True(
            res.Stdout.Contains("Live search via plugin:", StringComparison.OrdinalIgnoreCase)
            || res.Stdout.Contains("Not authenticated", StringComparison.OrdinalIgnoreCase),
            $"Unexpected output: {res.Stdout}\nStderr: {res.Stderr}");
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task Search_Unknown_Key_Shows_Allowed()
    {
        CliResult res = await RunCliAsync(["search", "Foo=bar"]);
        Assert.Contains("Unknown key(s): Foo. Allowed: Query", res.Stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task DownloadTrack_Unknown_Key_Shows_Allowed()
    {
        CliResult res = await RunCliAsync(["download-track", "Foo=1"]);
        Assert.Contains("Unknown key(s): Foo. Allowed:", res.Stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TrackId", res.Stdout);
        Assert.Contains("OutputDir", res.Stdout);
        Assert.Contains("Quality", res.Stdout);
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task DownloadTrack_Invalid_Quality_Shows_Message()
    {
        string outDir = Path.Combine(Temp, "tidalarr-cli-test-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(outDir);
        try
        {
            CliResult res = await RunCliAsync(["download-track", "TrackId=t1", $"OutputDir={outDir}", "Quality=Bad"]);
            Assert.Contains("Invalid Quality", res.Stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { try { Directory.Delete(outDir, true); } catch { } }
    }

    [Utils.CliFact]
    [Trait("scope", "cli")]
    public async Task DownloadAlbum_Missing_OutputDir_Shows_Usage()
    {
        CliResult res = await RunCliAsync(["download-album", "AlbumId=123"]);
        Assert.Contains("Usage: download-album", res.Stdout, StringComparison.OrdinalIgnoreCase);
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
        string cliDll = Path.Combine(RepoRoot, "TidalCLI", "bin", "Release", "net9.0", "TidalCLI.dll");

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
