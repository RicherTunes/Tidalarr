using System.IO;
using System.Linq;

using Xunit;

namespace Tidalarr.Tests;

/// <summary>
/// Regression guard: the OAuth-callback parse error log can echo the raw
/// redirect URL, which always carries <c>?code=...&amp;state=...</c>. The
/// log call must route the message through <c>LogRedactor.Redact</c>.
/// </summary>
public sealed class TidalLogRedactionTests
{
    [Fact]
    public void TidalLidarrIndexer_RedirectUrlParseError_Routes_Through_LogRedactor()
    {
        var sourcePath = ResolveSourcePath("Integration", "LidarrNative", "TidalLidarrIndexer.cs");
        var lines = File.ReadAllLines(sourcePath);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var isLogCall = line.Contains("_logger.") || line.Contains("this._logger.") || line.Contains("Logger.");
            if (!isLogCall) continue;

            // Any log line that interpolates callbackResult.ErrorMessage must
            // pass it through LogRedactor.Redact, since the message can echo
            // the raw URL with the OAuth code/state query parameters.
            if (line.Contains("callbackResult.ErrorMessage") && !line.Contains("LogRedactor.Redact"))
            {
                Assert.Fail($"TidalLidarrIndexer.cs:{i + 1} logs callbackResult.ErrorMessage without LogRedactor.Redact. Line: {line.Trim()}");
            }
        }
    }

    [Fact]
    public void TidalLidarrIndexer_ValidationFailure_Strings_Route_Exception_Through_LogRedactor()
    {
        // ValidationFailure messages flow to the Lidarr UI and forum-pasted
        // logs. They are NOT log calls but ARE a leak surface — exception
        // messages embedded in user-facing strings must route through the
        // redactor just like log calls do.
        var sourcePath = ResolveSourcePath("Integration", "LidarrNative", "TidalLidarrIndexer.cs");
        var lines = File.ReadAllLines(sourcePath);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains("ValidationFailure")) continue;

            // Look at the current line plus the next 2 lines (multi-line ctor calls)
            var window = string.Join("\n", lines.Skip(i).Take(3));
            if (System.Text.RegularExpressions.Regex.IsMatch(window, @"\b\w+\.Message\b") &&
                !window.Contains("LogRedactor.Redact"))
            {
                Assert.Fail($"TidalLidarrIndexer.cs:{i + 1} embeds exception.Message in a ValidationFailure without LogRedactor.Redact. Window: {window.Trim()}");
            }
        }
    }

    private static string ResolveSourcePath(params string[] subPath)
    {
        // Walk up until we find the tidalarr repo root, marked by the
        // top-level Tidalarr.sln file.
        var dir = new DirectoryInfo(typeof(TidalLogRedactionTests).Assembly.Location).Parent;
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Tidalarr.sln")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        var full = Path.Combine(new[] { dir!.FullName, "src", "Tidalarr" }.Concat(subPath).ToArray());
        Assert.True(File.Exists(full), $"expected source at {full}");
        return full;
    }
}
