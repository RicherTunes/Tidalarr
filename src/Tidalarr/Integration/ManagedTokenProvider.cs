using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration;

// IStreamingTokenProvider implementation backed by StreamingTokenManager.
internal sealed class ManagedTokenProvider(
    StreamingTokenManager<TidalTokens, TidalCredentials> manager,
    IServiceProvider services) : IStreamingTokenProvider
{
    private readonly StreamingTokenManager<TidalTokens, TidalCredentials> manager = manager;
    private readonly IServiceProvider services = services;
    private readonly object refreshSingleFlightLock = new();
    private Task<string>? refreshSingleFlight;

    public bool SupportsRefresh => true;
    public string ServiceName => "Tidal";

    public async Task<string> GetAccessTokenAsync()
    {
        TidalTokens session = await this.manager.GetValidSessionAsync(GetCredentials()).ConfigureAwait(false);
        return session.AccessToken ?? string.Empty;
    }

    public Task<string> RefreshTokenAsync()
    {
        lock (this.refreshSingleFlightLock)
        {
            this.refreshSingleFlight ??= RefreshTokenCoreAsync();
            return AwaitRefreshSingleFlightAsync(this.refreshSingleFlight);
        }
    }

    private async Task<string> AwaitRefreshSingleFlightAsync(Task<string> refreshTask)
    {
        try
        {
            return await refreshTask.ConfigureAwait(false);
        }
        finally
        {
            if (refreshTask.IsCompleted)
            {
                lock (this.refreshSingleFlightLock)
                {
                    if (ReferenceEquals(this.refreshSingleFlight, refreshTask))
                    {
                        this.refreshSingleFlight = null;
                    }
                }
            }
        }
    }

    private async Task<string> RefreshTokenCoreAsync()
    {
        try
        {
            if (this.services.GetService<ITidalAuth>() is IStreamingTokenProvider tidalTokenProvider &&
                tidalTokenProvider.SupportsRefresh)
            {
                string refreshedAccessToken = await tidalTokenProvider.RefreshTokenAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(refreshedAccessToken))
                {
                    this.manager.ClearSession();
                    try
                    {
                        await this.manager.RefreshSessionAsync(GetCredentials()).ConfigureAwait(false);
                    }
                    catch
                    {
                        // OAuth state is already refreshed. If Common cannot re-prime now,
                        // the next GetAccessTokenAsync call will reload from the OAuth service.
                    }

                    return refreshedAccessToken;
                }
            }

            await this.manager.RefreshSessionAsync(GetCredentials()).ConfigureAwait(false);
            TidalTokens session = await this.manager.GetValidSessionAsync(GetCredentials()).ConfigureAwait(false);
            return session.AccessToken ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
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
        this.manager.ClearSession();
    }

    internal static TidalCredentials GetCredentials(IServiceProvider services)
    {
        // Prefer aggregated settings if present; otherwise fallback to indexer settings.
        TidalarrSettings? agg = services.GetService<TidalarrSettings>();
        if (agg is not null)
        {
            return new TidalCredentials(agg.RedirectUrl);
        }

        TidalIndexerSettings? idx = services.GetService<TidalIndexerSettings>();
        if (idx is not null)
        {
            return new TidalCredentials(idx.RedirectUrl);
        }

        // As a last resort, provide a non-empty default to pass validation; authentication will still rely on persisted tokens.
        return new TidalCredentials("https://tidal.com/callback");
    }

    private TidalCredentials GetCredentials()
    {
        return GetCredentials(this.services);
    }
}
