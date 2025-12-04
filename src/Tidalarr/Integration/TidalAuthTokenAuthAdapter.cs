using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration;

// Adapts the existing ITidalAuth to the token manager's authentication interface.
internal sealed class TidalAuthTokenAuthAdapter(ITidalAuth auth) : IStreamingTokenAuthenticationService<TidalTokens, TidalCredentials>
{
    private readonly ITidalAuth auth = auth;

    public async Task<TidalTokens> AuthenticateAsync(TidalCredentials credentials)
    {
        // ITidalAuth internally loads/refreshes persisted tokens.
        return await this.auth.GetValidTokensAsync().ConfigureAwait(false);
    }

    public Task<bool> ValidateSessionAsync(TidalTokens session)
    {
        bool valid = session is not null && !session.IsExpired;
        return Task.FromResult(valid);
    }
}

