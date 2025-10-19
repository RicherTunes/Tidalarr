using FluentValidation;
using Lidarr.Plugin.Common.Utilities;

namespace Tidalarr.Integration;

internal static class PathValidationExtensions
{
    // Delegate to Common's permissive, cross-platform path sanity check
    public static bool IsReasonablePath(string? path) => PathValidation.IsReasonablePath(path);

    public static IRuleBuilderOptions<T, string> IsValidPath<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(IsReasonablePath).WithMessage("Path is invalid");
}


