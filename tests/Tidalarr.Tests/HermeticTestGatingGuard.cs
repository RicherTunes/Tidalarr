using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Tidalarr.Tests;

/// <summary>
/// Guards the "host-free hermetic OAuth test silently dropped from CI" failure mode. Tidalarr.Tests.csproj
/// does <c>&lt;Compile Remove="Tidal*.cs" /&gt;</c> under <c>ExcludeHostBridge=true</c> (forced by Common's
/// local-ci) and re-includes the host-free OAuth tests one-by-one. A new <c>TidalOAuthService*Tests.cs</c>
/// that someone forgets to re-include still compiles + passes LOCALLY (no ExcludeHostBridge) but is silently
/// skipped in CI — a green build that never ran the test (exactly what happened to
/// TidalOAuthServiceMissingRefreshTokenTests).
///
/// <para>This guard fails if any <c>TidalOAuthService*Tests.cs</c> source file (the host-free OAuth hermetic
/// family, by convention) lacks a correspondingly-named compiled type in this assembly. The guard file itself
/// is deliberately NOT named <c>Tidal*</c> so the broad remove never drops it. It locates the project dir from
/// the real runtime <see cref="Assembly.Location"/> (not <c>[CallerFilePath]</c>, which the build's path-map
/// can rewrite), and skips gracefully when the source tree isn't co-located (a packaged test run).</para>
/// </summary>
public class HermeticTestGatingGuard
{
    [Fact]
    public void AllHermeticOAuthServiceTests_AreCompiledIntoThisAssembly()
    {
        string? projectDir = FindTestProjectDir();
        if (projectDir is null)
        {
            return; // source tree not found next to the assembly (packaged run) — cannot verify; skip.
        }

        string[] sourceFiles = Directory.GetFiles(projectDir, "TidalOAuthService*Tests.cs");
        Assert.True(sourceFiles.Length > 0,
            "Expected at least one TidalOAuthService*Tests.cs hermetic source file in the test project.");

        var compiledTypeNames = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        string[] missing = sourceFiles
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name) && !compiledTypeNames.Contains(name!))
            .ToArray()!;

        Assert.True(missing.Length == 0,
            "Host-free OAuth hermetic test file(s) exist in source but are NOT compiled into the test assembly " +
            "— add a `<Compile Include=\"<file>.cs\" />` after the `Tidal*.cs` remove in Tidalarr.Tests.csproj " +
            "(these must run under the ExcludeHostBridge=true CI build). Missing: " + string.Join(", ", missing));
    }

    private static string? FindTestProjectDir()
    {
        string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "Tidalarr.Tests.csproj")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
