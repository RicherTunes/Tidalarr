using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Interfaces;

namespace Tidalarr.Integration
{
    // Adapts ITidalAuth to IStreamingTokenProvider for OAuthDelegatingHandler when a stub is used.
    internal class OAuthTokenProviderAdapter(ITidalAuth auth) : IStreamingTokenProvider
    {
        private readonly ITidalAuth _auth = auth;
        private string? _cachedToken;
        private DateTime? _cachedExpiry;

        public async Task<string> GetAccessTokenAsync()
        {
            try
            {
                Core.Models.TidalTokens t = await this._auth.GetValidTokensAsync();
                CacheExpiry(t);
                return t.AccessToken;
            }
            catch { return string.Empty; }
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
                CacheExpiry(refreshed);
                return refreshed.AccessToken;
            }
            catch { return string.Empty; }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                Core.Models.TidalTokens t = await this._auth.GetValidTokensAsync();
                CacheExpiry(t);
                return !string.IsNullOrEmpty(token) && t.AccessToken == token;
            }
            catch { return false; }
        }

        public DateTime? GetTokenExpiration(string token)
        {
            return token == _cachedToken ? _cachedExpiry : null;
        }

        public void ClearAuthenticationCache()
        {
            _cachedToken = null;
            _cachedExpiry = null;
        }

        public bool SupportsRefresh => true;
        public string ServiceName => "Tidal";

        private void CacheExpiry(Core.Models.TidalTokens tokens)
        {
            _cachedToken = tokens.AccessToken;
            _cachedExpiry = tokens.ExpiresAt;
        }
    }
}


