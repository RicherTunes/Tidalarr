using System.Text.RegularExpressions;

namespace Tidalarr.Tests.Unit.Packaging;

internal sealed partial record PackagingPolicyBaseline(
    IReadOnlyCollection<string> RequiredAssemblies,
    IReadOnlyCollection<string> OptionalAssemblies,
    IReadOnlyCollection<string> ForbiddenAssemblies)
{
    public static PackagingPolicyBaseline Default { get; } = new(
        RequiredAssemblies:
        [
            "Lidarr.Plugin.Abstractions.dll",
            "Lidarr.Plugin.Tidalarr.dll"
        ],
        OptionalAssemblies:
        [
            "Lidarr.Plugin.Common.dll"
        ],
        ForbiddenAssemblies:
        [
            "FluentValidation.dll",
            "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
            "Microsoft.Extensions.Logging.Abstractions.dll",
            "System.Text.Json.dll",
            "NLog.dll",
            "Lidarr.Core.dll",
            "Lidarr.Common.dll",
            "Lidarr.Host.dll",
            "Lidarr.Http.dll",
            "Lidarr.Api.V1.dll",
            "NzbDrone.Core.dll",
            "NzbDrone.Common.dll"
        ]);

    public static PackagingPolicyBaseline LoadOrDefault(string? baselinePath)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || !File.Exists(baselinePath))
        {
            return Default;
        }

        HashSet<string> required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> optional = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            RequiredAssemblies: required.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            OptionalAssemblies: optional.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            ForbiddenAssemblies: forbidden.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
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
