using System.Threading.Tasks;
using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration;

// Adapts the existing ITidalAuth to the token manager's authentication interface.
internal sealed class TidalAuthTokenAuthAdapter : IStreamingTokenAuthenticationService<TidalTokens, TidalCredentials>
{
    private readonly ITidalAuth auth;

    public TidalAuthTokenAuthAdapter(ITidalAuth auth)
    {
        this.auth = auth;
    }

    public async Task<TidalTokens> AuthenticateAsync(TidalCredentials credentials)
    {
        // ITidalAuth internally loads/refreshes persisted tokens.
        return await auth.GetValidTokensAsync().ConfigureAwait(false);
    }

    public Task<bool> ValidateSessionAsync(TidalTokens session)
    {
        var valid = session is not null && !session.IsExpired;
        return Task.FromResult(valid);
    }
}

