using FluentValidation.Results;
using Lidarr.Plugin.Abstractions.Contracts;

namespace Tidalarr.Integration;

internal static class ValidationExtensions
{
    public static PluginValidationResult ToPluginValidationResult(this ValidationResult result)
    {
        if (result == null)
        {
            return PluginValidationResult.Failure(new[] { "Validation result was null." });
        }

        if (result.IsValid)
        {
            return PluginValidationResult.Success();
        }

        string[] errors = result.Errors
            .Where(e => !string.IsNullOrWhiteSpace(e.ErrorMessage))
            .Select(e => e.ErrorMessage)
            .ToArray();

        return PluginValidationResult.Failure(errors);
    }
}
