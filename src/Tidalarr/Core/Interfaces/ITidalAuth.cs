using Tidalarr.Core.Models;

namespace Tidalarr.Core.Interfaces;

public interface ITidalAuth
{
    Task<TidalAuthUrl> GenerateAuthUrlAsync();
    Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier);
    Task<TidalTokens> RefreshTokensAsync(string refreshToken);
    Task<TidalTokens> GetValidTokensAsync();
    TidalCallbackResult ParseCallbackUrl(string callbackUrl);
    bool IsAuthenticated { get; }
}


