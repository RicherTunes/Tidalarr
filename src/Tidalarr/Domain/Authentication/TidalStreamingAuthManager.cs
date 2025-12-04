using Lidarr.Plugin.Common.Services.Authentication;
using Tidalarr.Core.Interfaces;

namespace Tidalarr.Domain.Authentication;

// Minimal IStreamingAuthManager implementation that reuses the existing Tidal OAuth service
// to ensure a valid session is available before API calls.
public sealed class TidalStreamingAuthManager(ITidalAuth authService) : IStreamingAuthManager
{
    private readonly ITidalAuth authService = authService;

    public async Task EnsureValidSessionAsync()
    {
        // Forces validation/refresh if needed; exceptions bubble to caller
        _ = await this.authService.GetValidTokensAsync().ConfigureAwait(false);
    }
}
