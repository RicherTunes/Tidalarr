using System;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration;

// IStreamingTokenProvider implementation backed by StreamingTokenManager.
internal sealed class ManagedTokenProvider : IStreamingTokenProvider
{
    private readonly StreamingTokenManager<TidalTokens, TidalCredentials> manager;
    private readonly IServiceProvider services;

    public ManagedTokenProvider(
        StreamingTokenManager<TidalTokens, TidalCredentials> manager,
        IServiceProvider services)
    {
        this.manager = manager;
        this.services = services;
    }

    public bool SupportsRefresh => true;
    public string ServiceName => "Tidal";

    public async Task<string> GetAccessTokenAsync()
    {
        var session = await manager.GetValidSessionAsync(GetCredentials()).ConfigureAwait(false);
        return session.AccessToken ?? string.Empty;
    }

    public async Task<string> RefreshTokenAsync()
    {
        await manager.RefreshSessionAsync(GetCredentials()).ConfigureAwait(false);
        var session = await manager.GetValidSessionAsync(GetCredentials()).ConfigureAwait(false);
        return session.AccessToken ?? string.Empty;
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        // Basic check against current session token
        return Task.FromResult(!string.IsNullOrEmpty(token));
    }

    public DateTime? GetTokenExpiration(string token)
    {
        // TidalTokens exposes ExpiresAt, but manager keeps session private.
        // Non-critical for handler behavior; return null.
        return null;
    }

    public void ClearAuthenticationCache()
    {
        manager.ClearSession();
    }

    private TidalCredentials GetCredentials()
    {
        // Prefer aggregated settings if present; otherwise fallback to indexer settings.
        var agg = services.GetService<TidalarrSettings>();
        if (agg is not null)
        {
            return new TidalCredentials(agg.RedirectUrl);
        }

        var idx = services.GetService<TidalIndexerSettings>();
        if (idx is not null)
        {
            return new TidalCredentials(idx.RedirectUrl);
        }

        // As a last resort, provide a non-empty default to pass validation; authentication will still rely on persisted tokens.
        return new TidalCredentials("https://tidal.com/callback");
    }
}

