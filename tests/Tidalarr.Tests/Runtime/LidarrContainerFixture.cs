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
/// xUnit collection fixture that boots a real Lidarr container with the merged
/// Tidalarr plugin DLL mounted into <c>/config/plugins/RicherTunes/Tidalarr</c>,
/// waits for the API to become healthy, and exposes the API key + HTTP client
/// to all tests in the <see cref="LidarrContainerCollection"/>.
///
/// The fixture is shared across the entire E2E test class so the (slow) container
/// startup happens exactly once per test run. It self-skips (sets
/// <see cref="SkipReason"/>) when Docker isn't available, the plugin DLL hasn't
/// been built, or the build is missing host-bridge artifacts.
///
/// Wave 21 — extracted from the in-class harness in DockerSmokeTests.cs so the
/// same orchestration powers a growing matrix of E2E smoke tests
/// (indexer schema, downloadclient schema, indexer test, downloadclient test).
/// Wave 22 will replicate this pattern in applemusicarr / qobuzarr / brainarr.
/// </summary>
public sealed class LidarrContainerFixture : IAsyncLifetime
{
    public const string ContainerName = "tidalarr-e2e";
    public const string DockerImage = "ghcr.io/hotio/lidarr:pr-plugins-3.1.2.4913";
    public const int LidarrPort = 8690; // Single-plugin instance per CLAUDE.md guidance
    private const int StartupTimeoutSeconds = 90;

    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromSeconds(10) };
    public string? ApiKey { get; private set; }
    public string BaseUrl => $"http://localhost:{LidarrPort}";

    /// <summary>
    /// When non-null, all tests in the collection should call <see cref="Skip.If(bool, string)"/>
    /// against this value — the fixture wasn't able to bring up a container.
    /// </summary>
    public string? SkipReason { get; private set; }

    private bool _containerStarted;

    public async Task InitializeAsync()
    {
        if (!DockerAvailable())
        {
            SkipReason = "Docker engine not running";
            return;
        }

        string? dllPath = FindPluginDll();
        if (dllPath is null)
        {
            SkipReason = "Plugin DLL not found. Build with: pwsh scripts/verify-local.ps1";
            return;
        }

        if (!IsHostBridgeBuild(dllPath))
        {
            SkipReason = "Plugin built with SkipHostBridge=true (FV 11.x). " +
                         "E2E requires a host-bridge build. Run: pwsh scripts/verify-local.ps1 -IncludeSmoke";
            return;
        }

        // Forcefully remove any leftover container from a previous run
        RunDocker($"rm -f {ContainerName}");

        string pluginDir = Path.GetDirectoryName(dllPath)!.Replace("\\", "/");
        string runArgs =
            $"run -d --name {ContainerName} " +
            $"-p {LidarrPort}:8686 " +
            $"-v \"{pluginDir}:/config/plugins/RicherTunes/Tidalarr\" " +
            DockerImage;

        (int exitCode, string output) = RunDocker(runArgs);
        if (exitCode != 0)
        {
            SkipReason = $"docker run failed (exit {exitCode}): {output}";
            return;
        }

        _containerStarted = true;

        try
        {
            ApiKey = await WaitForLidarrStartupAsync();
        }
        catch (Exception ex)
        {
            SkipReason = $"Lidarr did not become healthy: {ex.Message}";
        }
    }

    public Task DisposeAsync()
    {
        if (_containerStarted)
        {
            try { RunDocker($"rm -f {ContainerName}"); } catch { /* best effort */ }
            _containerStarted = false;
        }

        Http.Dispose();
        return Task.CompletedTask;
    }

    // -- Diagnostics -----------------------------------------------------

    public string GetContainerLogs()
    {
        (_, string logs) = RunDocker($"logs {ContainerName}");
        return logs;
    }

    // -- Internal helpers (mirror of the original DockerSmokeTests harness) ----

    private async Task<string> WaitForLidarrStartupAsync()
    {
        string initUrl = $"{BaseUrl}/initialize.json";
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(StartupTimeoutSeconds));
        string? apiKey = null;

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                if (apiKey is null)
                {
                    string initJson = await Http.GetStringAsync(initUrl, cts.Token);
                    using JsonDocument initDoc = JsonDocument.Parse(initJson);
                    if (initDoc.RootElement.TryGetProperty("apiKey", out JsonElement apiKeyEl))
                    {
                        apiKey = apiKeyEl.GetString();
                    }
                }

                if (apiKey is not null)
                {
                    string statusUrl = $"{BaseUrl}/api/v1/system/status?apikey={apiKey}";
                    HttpResponseMessage response = await Http.GetAsync(statusUrl, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        return apiKey;
                    }
                }
            }
            catch when (!cts.Token.IsCancellationRequested)
            {
                // Lidarr not ready — retry
            }

            await Task.Delay(1000, cts.Token);
        }

        (_, string logs) = RunDocker($"logs {ContainerName}");
        throw new TimeoutException(
            $"Lidarr did not start within {StartupTimeoutSeconds}s. Container logs:\n{Truncate(logs, 3000)}");
    }

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
            return false;
        }
    }

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

    private static bool IsHostBridgeBuild(string dllPath)
    {
        string dir = Path.GetDirectoryName(dllPath)!;
        return File.Exists(Path.Combine(dir, "Lidarr.Plugin.Abstractions.dll"));
    }

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

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "... (truncated)";

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
}

/// <summary>
/// xUnit collection definition that lets all E2E tests share the single
/// <see cref="LidarrContainerFixture"/> instance.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LidarrContainerCollection : ICollectionFixture<LidarrContainerFixture>
{
    public const string Name = "LidarrContainer";
}
