using System;
using System.IO;
using FluentValidation;
using FluentValidation.Validators;

namespace Tidalarr.Integration;

internal static class PathValidationExtensions
{
    // Lightweight path check suitable for CLI/test use without host libs
    public static bool IsReasonablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            // Check for invalid chars and a reasonable root/drive
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root);
        }
        catch { return false; }
    }

    public static IRuleBuilderOptions<T, string> IsValidPath<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(IsReasonablePath).WithMessage("Path is invalid");
}

