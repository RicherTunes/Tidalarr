using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Interfaces;

namespace Tidalarr.Integration
{
    // Adapts ITidalAuth to IStreamingTokenProvider for OAuthDelegatingHandler when a stub is used.
    internal class OAuthTokenProviderAdapter(ITidalAuth auth) : IStreamingTokenProvider
    {
        private readonly ITidalAuth _auth = auth;

        public async Task<string> GetAccessTokenAsync()
        {
            try { return (await this._auth.GetValidTokensAsync()).AccessToken; } catch { return string.Empty; }
        }

        public async Task<string> RefreshTokenAsync()
        {
            try
            {
                Core.Models.TidalTokens tokens = await this._auth.GetValidTokensAsync();
                if (string.IsNullOrEmpty(tokens.RefreshToken))
                {
                    return string.Empty;
                }

                Core.Models.TidalTokens refreshed = await this._auth.RefreshTokensAsync(tokens.RefreshToken);
                return refreshed.AccessToken;
            }
            catch { return string.Empty; }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try { return !string.IsNullOrEmpty(token) && (await this._auth.GetValidTokensAsync()).AccessToken == token; } catch { return false; }
        }

        // SYNC-OVER-ASYNC: IStreamingTokenProvider.GetTokenExpiration is a synchronous interface contract.
        public DateTime? GetTokenExpiration(string token)
        {
            try { Core.Models.TidalTokens t = this._auth.GetValidTokensAsync().GetAwaiter().GetResult(); return t.AccessToken == token ? t.ExpiresAt : null; } catch { return null; }
        }

        public void ClearAuthenticationCache() { /* no-op for adapter */ }

        public bool SupportsRefresh => true;
        public string ServiceName => "Tidal";
    }
}


