using FluentValidation;
using Lidarr.Plugin.Common.Utilities;

namespace Tidalarr.Integration;

internal static class PathValidationExtensions
{
    // Delegate to Common's permissive, cross-platform path sanity check
    public static bool IsReasonablePath(string? path)
    {
        return PathValidation.IsReasonablePath(path);
    }

    public static IRuleBuilderOptions<T, string> IsValidPath<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(IsReasonablePath).WithMessage("Path is invalid");
    }
}


