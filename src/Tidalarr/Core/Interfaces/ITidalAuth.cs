using Tidalarr.Core.Models;

namespace Tidalarr.Core.Interfaces;

public interface ITidalAuth
{
    Task<TidalAuthUrl> GenerateAuthUrlAsync();
    Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier);
    Task<TidalTokens> RefreshTokensAsync(string refreshToken);
    Task<TidalTokens> GetValidTokensAsync();
    bool IsAuthenticated { get; }
}
