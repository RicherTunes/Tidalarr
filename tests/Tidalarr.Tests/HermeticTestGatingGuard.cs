using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
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

    /// <summary>
    /// Generalizes the OAuth-only guard to cover every file declared in the
    /// <c>ExcludeHostBridge=true</c> <c>&lt;Compile Include&gt;</c> list. The old guard hardcoded
    /// <c>TidalOAuthService*Tests.cs</c> — so any non-OAuth re-included file (e.g.
    /// <c>TidalTestPolicies.cs</c>) or any new hermetic test added to the include list in the
    /// future was not verified. This guard reads the csproj at runtime and checks ALL declared
    /// includes automatically, without needing to update the guard when new files are added.
    ///
    /// <para>Fail case caught: a <c>&lt;Compile Include="TidalFoo.cs" /&gt;</c> entry exists in the
    /// csproj but the primary class name inside <c>TidalFoo.cs</c> does not match the file name
    /// (typo, rename, class missing) — the old guard would silently pass; this guard fails with the
    /// file name so the developer knows to fix the class name or the include entry.</para>
    /// </summary>
    [Fact]
    public void AllDeclaredHermeticIncludes_AreCompiledIntoThisAssembly()
    {
        string? projectDir = FindTestProjectDir();
        if (projectDir is null)
        {
            return; // source tree not co-located with assembly (packaged run); skip.
        }

        string csprojPath = Path.Combine(projectDir, "Tidalarr.Tests.csproj");
        if (!File.Exists(csprojPath))
        {
            return; // no csproj found; skip.
        }

        // Parse the csproj and collect every <Compile Include="..."> in an ExcludeHostBridge=true ItemGroup.
        XDocument doc = XDocument.Load(csprojPath);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        List<string> declaredIncludes = doc.Root!
            .Elements(ns + "ItemGroup")
            .Where(ig =>
            {
                string? cond = (string?)ig.Attribute("Condition");
                return cond is not null &&
                       cond.Contains("ExcludeHostBridge", StringComparison.Ordinal) &&
                       cond.Contains("true", StringComparison.Ordinal);
            })
            .SelectMany(ig => ig.Elements(ns + "Compile"))
            .Select(c => (string?)c.Attribute("Include"))
            .Where(inc => inc is not null)
            .Select(inc => inc!)
            .ToList();

        Assert.True(declaredIncludes.Count > 0,
            "No <Compile Include> entries found in the ExcludeHostBridge=true ItemGroup of " +
            "Tidalarr.Tests.csproj. The csproj structure may have changed — review the " +
            "broad-remove / re-include pattern and update this guard accordingly.");

        HashSet<string> compiledTypeNames = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        // For each declared include, the primary type name must match the file name (by convention).
        // Internal and nested types are returned by GetTypes() so helpers like TidalTestPolicies are covered.
        string[] missing = declaredIncludes
            .Select(inc => Path.GetFileNameWithoutExtension(Path.GetFileName(inc)))
            .Where(name => !string.IsNullOrEmpty(name) && !compiledTypeNames.Contains(name!))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n)
            .ToArray()!;

        Assert.True(missing.Length == 0,
            "Host-free file(s) declared via <Compile Include> in the ExcludeHostBridge=true " +
            "ItemGroup of Tidalarr.Tests.csproj are NOT compiled into the test assembly. " +
            "Ensure the file exists, the primary class name matches the filename, and the " +
            "<Compile Include> path is correct. Missing type(s): " + string.Join(", ", missing));
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
