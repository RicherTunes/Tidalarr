using Lidarr.Plugin.Common.Interfaces;

namespace Tidalarr.Core.Models;

/// <summary>
/// Represents Tidal OAuth tokens and session information.
/// </summary>
public record TidalTokens(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    DateTime ExpiresAt,
    string SessionId,
    string CountryCode,
    string UserId) : IAuthSession
{
    /// <summary>
    /// Returns true if the token is expired or will expire within 5 minutes.
    /// </summary>
    public bool IsExpired => ExpiresAt <= DateTime.UtcNow.AddMinutes(5);

    /// <summary>
    /// When the session expires (nullable for interface compatibility).
    /// </summary>
    DateTime? IAuthSession.ExpiresAt => ExpiresAt;

    /// <summary>
    /// Service-specific metadata about the session.
    /// </summary>
    public Dictionary<string, object> Metadata => new()
    {
        ["SessionId"] = SessionId,
        ["CountryCode"] = CountryCode,
        ["UserId"] = UserId,
        ["RefreshToken"] = RefreshToken,
        ["TokenType"] = TokenType
    };
}
