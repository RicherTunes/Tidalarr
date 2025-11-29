namespace Tidalarr.Core.Models;

/// <summary>
/// Represents OAuth authorization URL and related data for PKCE flow.
/// </summary>
public record TidalAuthUrl(
    string AuthorizationUrl,
    string CodeVerifier,
    string State,
    string ClientUniqueKey);
