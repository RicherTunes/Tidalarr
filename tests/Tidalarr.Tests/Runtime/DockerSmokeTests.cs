using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tidalarr.Tests.Runtime;

/// <summary>
/// Docker-based smoke tests that mount the merged Lidarr.Plugin.Tidalarr.dll into a real
/// Lidarr container and verify the plugin loads, registers its indexer, and responds to API calls.
///
/// These tests require Docker engine to be running and are skipped gracefully when it is not.
/// Run with: dotnet test --filter "Category=Docker"
///
/// What this proves that sandbox tests cannot:
/// - The merged DLL loads inside the real Lidarr host process
/// - Plugin registers its indexer schema in the Lidarr API
/// - Plugin survives the full Lidarr startup lifecycle
///
/// Known limitation: Lidarr.Plugin.Abstractions.dll references FluentValidation 11.x
/// but the host ships FV 9.x. If the merged DLL was built with SkipHostBridge=true,
/// the plugin may fail to load due to this host-boundary conflict. Build with full
/// host assemblies (via verify-local.ps1) for a complete Docker smoke test.
/// </summary>
public class DockerSmokeTests : IDisposable
{
    private const string ContainerName = "tidalarr-smoke-test";
    private const string DockerImage = "ghcr.io/hotio/lidarr:pr-plugins-3.1.2.4913";
    private const int LidarrPort = 8686;
    private const int StartupTimeoutSeconds = 60;

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private bool _containerStarted;

    /// <summary>
    /// Checks whether the Docker engine is available by running <c>docker info</c>.
    /// </summary>
    private static bool DockerAvailable()
    {
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            bool exited = process.WaitForExit(10_000);
            return exited && process.ExitCode == 0;
        }
        catch
        {
            // docker binary not found or other OS-level error
            return false;
        }
    }

    /// <summary>
    /// Locates the ILRepack-merged plugin DLL in known build output paths.
    /// </summary>
    private static string? FindPluginDll()
    {
        string repoRoot = FindRepoRoot();
        string[] candidates =
        [
            Path.Combine(repoRoot, "src", "Tidalarr", "bin", "Lidarr.Plugin.Tidalarr.dll"),
            Path.Combine(repoRoot, "src", "Tidalarr", "bin", "Release", "Lidarr.Plugin.Tidalarr.dll"),
            Path.Combine(repoRoot, "src", "Tidalarr", "bin", "Debug", "Lidarr.Plugin.Tidalarr.dll"),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    [SkippableFact]
    [Trait("Category", "Docker")]
    public async Task Plugin_Loads_In_Real_Lidarr_Container()
    {
        Skip.If(!DockerAvailable(), "Docker engine not running");

        string? dllPath = FindPluginDll();
        Skip.If(dllPath is null,
            "Plugin DLL not found. Build with ILRepack first: dotnet build src/Tidalarr/Tidalarr.csproj -c Release");

        try
        {
            // Remove any leftover container from a previous run
            RunDocker($"rm -f {ContainerName}");

            // Start Lidarr with the plugin DLL mounted into the correct plugin directory.
            // Lidarr's plugin loader expects: /config/plugins/<owner>/<pluginName>/<dll>
            string mountSource = dllPath!.Replace("\\", "/");

            // Also mount Abstractions DLL if it exists alongside the merged DLL
            string dllDir = Path.GetDirectoryName(dllPath)!;
            string? abstractionsDll = Path.Combine(dllDir, "Lidarr.Plugin.Abstractions.dll");
            bool hasAbstractions = File.Exists(abstractionsDll);
            string abstractionsMount = hasAbstractions
                ? $"-v \"{abstractionsDll!.Replace("\\", "/")}:/config/plugins/RicherTunes/Tidalarr/Lidarr.Plugin.Abstractions.dll\" "
                : "";

            string runArgs =
                $"run -d --name {ContainerName} " +
                $"-p {LidarrPort}:{LidarrPort} " +
                $"-v \"{mountSource}:/config/plugins/RicherTunes/Tidalarr/Lidarr.Plugin.Tidalarr.dll\" " +
                abstractionsMount +
                DockerImage;

            (int exitCode, string output) = RunDocker(runArgs);
            Assert.True(exitCode == 0, $"docker run failed (exit {exitCode}): {output}");
            _containerStarted = true;

            // Wait for Lidarr to start by polling the system status endpoint
            string apiKey = await WaitForLidarrStartupAsync();

            // Query the indexer schema and verify our plugin registered
            string schemaUrl = $"http://localhost:{LidarrPort}/api/v1/indexer/schema?apikey={apiKey}";
            string schemaJson = await _http.GetStringAsync(schemaUrl);

            // The schema response is a JSON array of indexer definitions.
            // Look for one whose "name" or "implementation" contains "Tidal".
            using JsonDocument doc = JsonDocument.Parse(schemaJson);
            bool hasTidalIndexer = doc.RootElement.EnumerateArray().Any(element =>
            {
                string name = element.TryGetProperty("name", out JsonElement nameEl)
                    ? nameEl.GetString() ?? ""
                    : "";
                string implementation = element.TryGetProperty("implementation", out JsonElement implEl)
                    ? implEl.GetString() ?? ""
                    : "";
                return name.Contains("Tidal", StringComparison.OrdinalIgnoreCase)
                    || implementation.Contains("Tidal", StringComparison.OrdinalIgnoreCase);
            });

            Assert.True(hasTidalIndexer,
                $"Expected indexer schema to contain a Tidal indexer. Schema response: {Truncate(schemaJson, 2000)}");
        }
        finally
        {
            CleanupContainer();
        }
    }

    /// <summary>
    /// Polls Lidarr's initialize.json endpoint to retrieve the API key, then verifies
    /// the system/status endpoint responds. Retries until <see cref="StartupTimeoutSeconds"/>.
    /// </summary>
    private async Task<string> WaitForLidarrStartupAsync()
    {
        string initUrl = $"http://localhost:{LidarrPort}/initialize.json";
        string statusUrlTemplate = $"http://localhost:{LidarrPort}/api/v1/system/status?apikey={{0}}";

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(StartupTimeoutSeconds));
        string? apiKey = null;

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                // Step 1: Get the API key from initialize.json
                if (apiKey is null)
                {
                    string initJson = await _http.GetStringAsync(initUrl, cts.Token);
                    using JsonDocument initDoc = JsonDocument.Parse(initJson);
                    if (initDoc.RootElement.TryGetProperty("apiKey", out JsonElement apiKeyEl))
                    {
                        apiKey = apiKeyEl.GetString();
                    }
                }

                // Step 2: Verify system status responds (proves Lidarr is fully started)
                if (apiKey is not null)
                {
                    string statusUrl = string.Format(statusUrlTemplate, apiKey);
                    HttpResponseMessage response = await _http.GetAsync(statusUrl, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        return apiKey;
                    }
                }
            }
            catch (Exception) when (!cts.Token.IsCancellationRequested)
            {
                // Lidarr not ready yet — retry
            }

            await Task.Delay(1000, cts.Token);
        }

        // Capture container logs for diagnostics before failing
        (_, string logs) = RunDocker($"logs {ContainerName}");
        throw new TimeoutException(
            $"Lidarr did not start within {StartupTimeoutSeconds}s. Container logs:\n{Truncate(logs, 3000)}");
    }

    /// <summary>
    /// Runs a docker command synchronously and returns the exit code and combined output.
    /// </summary>
    private static (int ExitCode, string Output) RunDocker(string arguments)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(60_000);

        string combined = string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}";
        return (process.ExitCode, combined.Trim());
    }

    /// <summary>
    /// Forcefully removes the test container, ignoring any errors.
    /// </summary>
    private void CleanupContainer()
    {
        if (!_containerStarted) return;

        try
        {
            RunDocker($"rm -f {ContainerName}");
        }
        catch
        {
            // Best effort — container may already be gone
        }

        _containerStarted = false;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "... (truncated)";
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Tidalarr.sln")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return AppContext.BaseDirectory;
    }

    public void Dispose()
    {
        CleanupContainer();
        _http.Dispose();
    }
}
