using Lidarr.Plugin.Common.Interfaces;

namespace Tidalarr.Core.Models;

/// <summary>
/// Represents Tidal OAuth credentials configuration.
/// </summary>
public record TidalCredentials(string RedirectUrl) : IAuthCredentials
{
    /// <summary>
    /// The type of authentication (OAuth2 for Tidal).
    /// </summary>
    public AuthenticationType Type => AuthenticationType.OAuth2;

    /// <summary>
    /// Validates that the credentials are complete and properly formatted.
    /// </summary>
    public bool IsValid(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(RedirectUrl))
        {
            errorMessage = "Redirect URL is required for OAuth2 authentication.";
            return false;
        }

        if (!Uri.TryCreate(RedirectUrl, UriKind.Absolute, out _))
        {
            errorMessage = "Redirect URL must be a valid absolute URL.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
