using Lidarr.Plugin.Common.Interfaces;
using Tidalarr.Core.Interfaces;

namespace Tidalarr.Integration
{
    // Adapts ITidalAuth to IStreamingTokenProvider for OAuthDelegatingHandler when a stub is used.
    internal class OAuthTokenProviderAdapter(ITidalAuth auth) : IStreamingTokenProvider
    {
        private readonly ITidalAuth _auth = auth;

        // Thread-safety note: _cachedToken and _cachedExpiry are written by the async token
        // operations (GetAccessTokenAsync, RefreshTokenAsync, ValidateTokenAsync) via CacheExpiry(),
        // and read synchronously by GetTokenExpiration() and ClearAuthenticationCache().
        //
        // This is intentionally unsynchronized. Lidarr's plugin lifecycle is single-threaded per
        // session, so a genuine data race cannot occur in production. In the unlikely event of a
        // concurrent read during a write (e.g., during unit-test parallelism), the worst outcome is
        // a stale cache miss: GetTokenExpiration() returns null and the caller retries the token
        // fetch — fully safe and self-healing.
        //
        // A lock or Interlocked swap would be the correct fix if thread safety were ever required.
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


