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

        // Require explicit http(s) scheme. Note: on Linux, a path like "/callback"
        // is parsed as `file:///callback` by Uri.TryCreate(...UriKind.Absolute),
        // which is "absolute" but obviously not a usable OAuth redirect. Cross-platform
        // we must filter on scheme too.
        if (!Uri.TryCreate(RedirectUrl, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            errorMessage = "Redirect URL must be a valid absolute URL with http or https scheme.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
