using System.Text.RegularExpressions;

namespace Tidalarr.Tests.Unit.Packaging;

internal sealed partial record PackagingPolicyBaseline(
    IReadOnlyCollection<string> RequiredAssemblies,
    IReadOnlyCollection<string> OptionalAssemblies,
    IReadOnlyCollection<string> ForbiddenAssemblies)
{
    public static PackagingPolicyBaseline Default { get; } = new(
        RequiredAssemblies: new[]
        {
            "Lidarr.Plugin.Abstractions.dll",
            "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
            "Microsoft.Extensions.Logging.Abstractions.dll",
            "Lidarr.Plugin.Tidalarr.dll"
        },
        OptionalAssemblies: new[]
        {
            "Lidarr.Plugin.Common.dll"
        },
        ForbiddenAssemblies: new[]
        {
            "FluentValidation.dll",
            "System.Text.Json.dll",
            "Lidarr.Core.dll",
            "Lidarr.Common.dll",
            "Lidarr.Host.dll",
            "Lidarr.Http.dll",
            "Lidarr.Api.V1.dll",
            "NzbDrone.Core.dll",
            "NzbDrone.Common.dll"
        });

    public static PackagingPolicyBaseline LoadOrDefault(string? baselinePath)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || !File.Exists(baselinePath))
        {
            return Default;
        }

        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var optional = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var mode = Mode.None;
        foreach (var rawLine in File.ReadAllLines(baselinePath))
        {
            var line = rawLine.Trim();
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
                var value = match.Groups["value"].Value;
                if (!value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                switch (mode)
                {
                    case Mode.Required:
                        required.Add(value);
                        break;
                    case Mode.Optional:
                        optional.Add(value);
                        break;
                    case Mode.Forbidden:
                        forbidden.Add(value);
                        break;
                }
            }
        }

        if (required.Count == 0 && optional.Count == 0 && forbidden.Count == 0)
        {
            return Default;
        }

        return new PackagingPolicyBaseline(
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
