using System;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Interfaces;

namespace Tidalarr.Integration
{
    // Adapts ITidalAuth to IStreamingTokenProvider for OAuthDelegatingHandler when a stub is used.
    internal class OAuthTokenProviderAdapter : IStreamingTokenProvider
    {
        private readonly ITidalAuth _auth;
        public OAuthTokenProviderAdapter(ITidalAuth auth) => _auth = auth;

        public async Task<string> GetAccessTokenAsync()
        {
            try { return (await _auth.GetValidTokensAsync()).AccessToken; } catch { return string.Empty; }
        }

        public async Task<string> RefreshTokenAsync()
        {
            try
            {
                var tokens = await _auth.GetValidTokensAsync();
                if (string.IsNullOrEmpty(tokens.RefreshToken)) return string.Empty;
                var refreshed = await _auth.RefreshTokensAsync(tokens.RefreshToken);
                return refreshed.AccessToken;
            }
            catch { return string.Empty; }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try { return !string.IsNullOrEmpty(token) && (await _auth.GetValidTokensAsync()).AccessToken == token; } catch { return false; }
        }

        public DateTime? GetTokenExpiration(string token)
        {
            try { var t = _auth.GetValidTokensAsync().GetAwaiter().GetResult(); return t.AccessToken == token ? t.ExpiresAt : null; } catch { return null; }
        }

        public void ClearAuthenticationCache() { /* no-op for adapter */ }

        public bool SupportsRefresh => true;
        public string ServiceName => "Tidal";
    }
}


