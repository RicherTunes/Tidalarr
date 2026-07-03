using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using NzbDrone.Core.Annotations;
using Tidalarr.Integration.LidarrNative;
using Xunit;

namespace Tidalarr.Tests.Documentation;

/// <summary>
/// T-3 (external dead-settings audit, 2026-07): five settings were exposed in the Lidarr UI
/// (<c>[FieldDefinition]</c>) and documented in README.md, yet no runtime code ever read their
/// value — a user could change them and observe zero behavioral effect. This is a
/// "documentation-truth" guard in the spirit of <see cref="DocumentationTruthTests"/>: it fails
/// whenever an exposed setting has no real consumer, so the class of bug can't silently recur.
///
/// The guard is deliberately a source-text scan rather than a live host/DI assertion — it has no
/// dependency on constructing runtime services, so it stays fast and hermetic while still covering
/// the two settings classes Lidarr actually renders (<see cref="TidalLidarrIndexerSettings"/>,
/// <see cref="TidalLidarrDownloadClientSettings"/>).
///
/// A property only counts as "consumed" if it is referenced (<c>.PropertyName</c>) outside a small,
/// explicit allow-list of plumbing-only files (files whose entire job is to declare, validate, or
/// copy the setting to another DTO — e.g. the settings classes themselves, the runtime caches, and
/// TidalarrPlugin.cs's Common-IPlugin schema shim) AND outside a same-name copy idiom
/// (<c>PropName = x.PropName,</c> / <c>nameof(Type.PropName)</c>) wherever else it appears. Without
/// both exclusions this guard would be trivially green forever: every property here is already
/// copied between settings objects somewhere (that copying is exactly the "looks wired, does
/// nothing" trap T-3 flagged), including inside otherwise-legitimate consumer files like
/// TidalModule.cs (which back-compat-maps settings AND independently derives real behavioral
/// parameters like max download concurrency — so it can't be file-excluded wholesale). Real
/// consumption looks different: a differently-named local/derived value, a branch, a comparison —
/// e.g. TidalLidarrIndexer.cs, TidalEarlyReleaseFilter.cs, TidalResponseCache.cs.
///
/// Portable to the other four streaming plugins: swap the settings types + plumbing-file list.
/// </summary>
public class DeadSettingsGuardTests
{
    // Files whose only job is declaring / validating / copying settings values — never acting on
    // them. A property referenced ONLY inside these files has no proven runtime effect.
    private static readonly string[] PlumbingOnlyFiles =
    [
        "TidalLidarrIndexerSettings.cs",
        "TidalLidarrDownloadClientSettings.cs",
        "TidalIndexerSettings.cs",
        "TidalDownloadClientSettings.cs",
        "TidalarrSettings.cs",
        "TidalIndexerRuntimeCache.cs",
        "TidalDownloadClientRuntimeCache.cs",
        "TidalarrPlugin.cs",
    ];

    // Settings whose "consumer" IS the getter itself — a computed/self-implementing property with
    // no other call site to find by name. Verified real per CLAUDE.md's "OAuth Authorization URL
    // Field (Do Not Remove)" section: the getter calls PKCEStateStore.TryGetOrCreateAuthorizationUrl
    // directly, so there is no separate ".OAuthAuthUrl" read anywhere else to match against. Keep
    // this list tiny and require a documented rationale for every entry.
    private static readonly string[] SelfImplementingConsumers = ["OAuthAuthUrl"];

    public static IEnumerable<object[]> ExposedSettingsTypes()
    {
        yield return [typeof(TidalLidarrIndexerSettings)];
        yield return [typeof(TidalLidarrDownloadClientSettings)];
    }

    [Theory]
    [MemberData(nameof(ExposedSettingsTypes))]
    public void Every_ExposedSetting_HasARealNonTestConsumer(Type settingsType)
    {
        string srcRoot = Path.Combine(FindRepositoryRoot(), "src", "Tidalarr");
        List<string> sourceFiles = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin-tests{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}"))
            .ToList();

        List<(string File, string Content)> consumerCandidates = sourceFiles
            .Where(f => !PlumbingOnlyFiles.Contains(Path.GetFileName(f)))
            .Select(f => (File: f, Content: File.ReadAllText(f)))
            .ToList();

        List<string> exposedPropertyNames = settingsType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<FieldDefinitionAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        exposedPropertyNames.Should().NotBeEmpty(
            $"{settingsType.Name} should declare at least one [FieldDefinition] property — if this " +
            "fails, the reflection target/attribute type has drifted and the guard needs updating.");

        List<string> deadSettings = [];
        foreach (string propertyName in exposedPropertyNames)
        {
            if (SelfImplementingConsumers.Contains(propertyName))
            {
                continue;
            }

            bool hasConsumer = consumerCandidates.Any(c => HasRealConsumerReference(c.Content, propertyName));
            if (!hasConsumer)
            {
                deadSettings.Add(propertyName);
            }
        }

        deadSettings.Should().BeEmpty(
            $"every [FieldDefinition] property on {settingsType.Name} must be read by real behavior " +
            "somewhere outside settings-declaration/validation/copy-only files. Either wire the value " +
            "into a real consumer (search/download/auth/caching/etc.) or remove the [FieldDefinition] " +
            "(+ property if nothing else references it) instead of leaving a disclosure-only stub.");
    }

    /// <summary>
    /// True if <paramref name="content"/> references <c>PropertyName</c> — either a direct member
    /// read (<c>.PropertyName</c>) or a call to a derived/effective accessor whose name ends with it
    /// (e.g. <c>GetEffectiveMaxConcurrentChunkDownloads()</c>) — on some line that is NOT a same-name
    /// copy idiom: <c>PropertyName = x.PropertyName,</c> or <c>x.PropertyName = PropertyName;</c> or
    /// <c>nameof(Type.PropertyName)</c>. Assigning the value straight through under the identical
    /// name (or citing it as a compile-time string via <c>nameof</c>) proves nothing was ever read
    /// for a decision — just relayed. A differently named local/derived value, a comparison, a
    /// branch, or a method argument all count as real consumption.
    /// </summary>
    private static bool HasRealConsumerReference(string content, string propertyName)
    {
        string escaped = Regex.Escape(propertyName);
        var occurrence = new Regex($@"\.{escaped}\b|\w*{escaped}\s*\(");
        var nonConsumingLine = new Regex(
            $@"^\s*(?:\w+\??\.)?{escaped}\s*=\s*[\w?.]*\.{escaped}\s*[,;]?\s*$|nameof\(\s*[\w.]*\.?{escaped}\s*\)");

        foreach (string line in content.Split('\n'))
        {
            if (occurrence.IsMatch(line) && !nonConsumingLine.IsMatch(line))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "Tidalarr.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException($"Could not locate repo root from {AppContext.BaseDirectory}");
    }
}
