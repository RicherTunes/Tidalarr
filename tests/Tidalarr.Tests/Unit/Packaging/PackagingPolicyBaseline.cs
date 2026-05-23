using System.Text.RegularExpressions;

namespace Tidalarr.Tests.Unit.Packaging;

internal sealed partial record PackagingPolicyBaseline(
    IReadOnlyCollection<string> RequiredAssemblies,
    IReadOnlyCollection<string> OptionalAssemblies,
    IReadOnlyCollection<string> ForbiddenAssemblies)
{
    // Previously RequiredAssemblies also included Lidarr.Plugin.Abstractions.dll and
    // OptionalAssemblies included Lidarr.Plugin.Common.dll. Both are now merged +
    // internalized into Lidarr.Plugin.Tidalarr.dll via ILRepack (see
    // ext/Lidarr.Plugin.Common/build/PluginPackaging.targets — May 2026, when
    // multi-plugin co-existence pushed the merge to fix COR_E_INVALIDOPERATION).
    // Shipping them as sidecars regresses the merge, so they now belong in the
    // forbidden list. The companion Plugin_Dll_Should_Be_Merged_Size test guards
    // against the inverse failure mode (merge didn't run, sidecars correctly
    // omitted, runtime fails with "Could not load Common / Abstractions").
    public static PackagingPolicyBaseline Default { get; } = new(
        RequiredAssemblies:
        [
            "Lidarr.Plugin.Tidalarr.dll"
        ],
        OptionalAssemblies:
        [
            // No sidecars — Common + Abstractions are merged.
        ],
        ForbiddenAssemblies:
        [
            "FluentValidation.dll",
            "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
            "Microsoft.Extensions.Logging.Abstractions.dll",
            "Microsoft.Extensions.Caching.Abstractions.dll",
            "Microsoft.Extensions.Caching.Memory.dll",
            "Microsoft.Extensions.Options.dll",
            "Microsoft.Extensions.Primitives.dll",
            "System.Text.Json.dll",
            "Newtonsoft.Json.dll",
            "NLog.dll",
            "Lidarr.Core.dll",
            "Lidarr.Common.dll",
            "Lidarr.Host.dll",
            "Lidarr.Http.dll",
            "Lidarr.Api.V1.dll",
            "NzbDrone.Core.dll",
            "NzbDrone.Common.dll",
            // Plugin abstractions — merged + internalized by ILRepack.
            // Were in RequiredAssemblies / OptionalAssemblies before the May 2026 merge.
            "Lidarr.Plugin.Abstractions.dll",
            "Lidarr.Plugin.Common.dll"
        ]);

    public static PackagingPolicyBaseline LoadOrDefault(string? baselinePath)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || !File.Exists(baselinePath))
        {
            return Default;
        }

        HashSet<string> required = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> optional = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> forbidden = new(StringComparer.OrdinalIgnoreCase);

        Mode mode = Mode.None;
        foreach (string rawLine in File.ReadAllLines(baselinePath))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("Required", StringComparison.OrdinalIgnoreCase))
            {
                mode = Mode.Required;
                continue;
            }

            if (line.StartsWith("Kept", StringComparison.OrdinalIgnoreCase))
            {
                mode = Mode.Optional;
                continue;
            }

            if (line.StartsWith("Forbidden", StringComparison.OrdinalIgnoreCase))
            {
                mode = Mode.Forbidden;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                mode = Mode.None;
                continue;
            }

            if (mode == Mode.None)
            {
                continue;
            }

            foreach (Match match in BacktickedValueRegex().Matches(line))
            {
                string value = match.Groups["value"].Value;
                if (!value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                switch (mode)
                {
                    case Mode.Required:
                        _ = required.Add(value);
                        break;
                    case Mode.Optional:
                        _ = optional.Add(value);
                        break;
                    case Mode.Forbidden:
                        _ = forbidden.Add(value);
                        break;
                    case Mode.None:
                        break;
                    default:
                        break;
                }
            }
        }

        return required.Count == 0 && optional.Count == 0 && forbidden.Count == 0
            ? Default
            : new PackagingPolicyBaseline(
            RequiredAssemblies: [.. required.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)],
            OptionalAssemblies: [.. optional.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)],
            ForbiddenAssemblies: [.. forbidden.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)]);
    }

    private enum Mode
    {
        None,
        Required,
        Optional,
        Forbidden
    }

    [GeneratedRegex("`(?<value>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex BacktickedValueRegex();
}
