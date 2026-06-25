using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

[Trait("Category", "Integration")]
[Trait("Area", "E2E/Hermetic")]
public class ManagedTokenProviderRenewalTests
{
    [Fact]
    public async Task GetAccessTokenAsync_WhenPersistedTidalTokenExpired_RefreshesUsingStoredRefreshToken()
    {
        TidalTokens expired = new(
            "old-access",
            "refresh-token",
            "Bearer",
            DateTime.UtcNow.AddMinutes(-10),
            "session-old",
            "US",
            "user-1");
        RecordingTokenStore store = new(expired);
        StoreBackedRefreshAuthService authService = new(store);

        using ServiceProvider provider = BuildProvider(store, authService);

        IStreamingTokenProvider tokenProvider = provider.GetRequiredService<IStreamingTokenProvider>();
        string accessToken = await tokenProvider.GetAccessTokenAsync();

        Assert.Equal("new-access-1", accessToken);
        Assert.Equal(1, authService.AuthenticateCalls);

        TokenEnvelope<TidalTokens>? persisted = await store.LoadAsync();
        Assert.NotNull(persisted);
        Assert.Equal("new-refresh-1", persisted!.Session.RefreshToken);
        Assert.Equal(0, store.ClearCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenCachedTidalTokenInsideRefreshBuffer_RefreshesInsteadOfServingStaleToken()
    {
        RecordingTokenStore store = new();
        ShortLivedAuthService authService = new();

        using ServiceProvider provider = BuildProvider(store, authService);

        IStreamingTokenProvider tokenProvider = provider.GetRequiredService<IStreamingTokenProvider>();
        string first = await tokenProvider.GetAccessTokenAsync();
        string second = await tokenProvider.GetAccessTokenAsync();

        Assert.Equal("short-access-1", first);
        Assert.Equal("short-access-2", second);
        Assert.Equal(2, authService.AuthenticateCalls);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenClockValidTokenRejected_RefreshesOAuthStateAndReprimesCommonManager()
    {
        RefreshableTidalAuth auth = new();

        using ServiceProvider provider = BuildProvider(auth);

        IStreamingTokenProvider tokenProvider = provider.GetRequiredService<IStreamingTokenProvider>();
        Assert.Equal("old-access", await tokenProvider.GetAccessTokenAsync());

        string refreshed = await tokenProvider.RefreshTokenAsync();
        string afterRefresh = await tokenProvider.GetAccessTokenAsync();

        Assert.Equal("new-access-1", refreshed);
        Assert.Equal("new-access-1", afterRefresh);
        Assert.Equal(1, auth.ProviderRefreshCalls);
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenConcurrentRejectedTokenRefreshes_SharesSingleUnderlyingRefresh()
    {
        RefreshableTidalAuth auth = new()
        {
            RefreshDelay = TimeSpan.FromMilliseconds(50)
        };

        using ServiceProvider provider = BuildProvider(auth);

        IStreamingTokenProvider tokenProvider = provider.GetRequiredService<IStreamingTokenProvider>();
        string[] refreshed = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => tokenProvider.RefreshTokenAsync()));

        Assert.All(refreshed, accessToken => Assert.Equal("new-access-1", accessToken));
        Assert.Equal(1, auth.ProviderRefreshCalls);
    }

    [Fact]
    public async Task ClearAuthenticationCache_DoesNotClearPersistedOAuthRefreshState()
    {
        RefreshableTidalAuth auth = new();

        using ServiceProvider provider = BuildProvider(auth);

        IStreamingTokenProvider tokenProvider = provider.GetRequiredService<IStreamingTokenProvider>();
        Assert.Equal("old-access", await tokenProvider.GetAccessTokenAsync());

        tokenProvider.ClearAuthenticationCache();

        Assert.Equal(0, auth.ClearAuthenticationCacheCalls);
        Assert.Equal("old-access", await tokenProvider.GetAccessTokenAsync());
    }

    [Fact]
    public void RegisterServices_TokenProviderGraph_PassesScopeValidation()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(new TidalarrSettings
        {
            ConfigPath = Path.Combine(Path.GetTempPath(), "tidalarr-token-provider-tests"),
            RedirectUrl = "https://tidal.com/android/login/auth"
        });

        TidalModule.RegisterServices(services);

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        Assert.NotNull(provider.GetRequiredService<IStreamingTokenProvider>());
    }

    private static ServiceProvider BuildProvider(
        RecordingTokenStore store,
        IStreamingTokenAuthenticationService<TidalTokens, TidalCredentials> authService)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(new TidalarrSettings
        {
            ConfigPath = Path.Combine(Path.GetTempPath(), "tidalarr-token-provider-tests"),
            RedirectUrl = "https://tidal.com/android/login/auth"
        });

        TidalModule.RegisterServices(services);

        services.Replace(ServiceDescriptor.Singleton<ITokenStore<TidalTokens>>(store));
        services.Replace(ServiceDescriptor.Singleton(authService));

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildProvider(ITidalAuth auth)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(new TidalarrSettings
        {
            ConfigPath = Path.Combine(Path.GetTempPath(), "tidalarr-token-provider-tests"),
            RedirectUrl = "https://tidal.com/android/login/auth"
        });

        TidalModule.RegisterServices(services);
        services.Replace(ServiceDescriptor.Singleton(auth));

        return services.BuildServiceProvider();
    }

    private sealed class RecordingTokenStore : ITokenStore<TidalTokens>
    {
        private TokenEnvelope<TidalTokens>? envelope;

        public RecordingTokenStore()
        {
        }

        public RecordingTokenStore(TidalTokens initial)
        {
            this.envelope = new TokenEnvelope<TidalTokens>(initial, initial.ExpiresAt);
        }

        public int ClearCount { get; private set; }

        public Task<TokenEnvelope<TidalTokens>?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.envelope);
        }

        public Task SaveAsync(TokenEnvelope<TidalTokens> envelope, CancellationToken cancellationToken = default)
        {
            this.envelope = envelope;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCount++;
            this.envelope = null;
            return Task.CompletedTask;
        }
    }

    private sealed class StoreBackedRefreshAuthService(RecordingTokenStore store)
        : IStreamingTokenAuthenticationService<TidalTokens, TidalCredentials>
    {
        private readonly RecordingTokenStore store = store;

        public int AuthenticateCalls { get; private set; }

        public async Task<TidalTokens> AuthenticateAsync(TidalCredentials credentials)
        {
            AuthenticateCalls++;

            TokenEnvelope<TidalTokens>? persisted = await this.store.LoadAsync();
            if (persisted?.Session is not { RefreshToken.Length: > 0 } stored)
            {
                throw new InvalidOperationException("No stored refresh token available.");
            }

            if (!stored.IsExpired)
            {
                return stored;
            }

            TidalTokens refreshed = stored with
            {
                AccessToken = $"new-access-{AuthenticateCalls}",
                RefreshToken = $"new-refresh-{AuthenticateCalls}",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            await this.store.SaveAsync(new TokenEnvelope<TidalTokens>(refreshed, refreshed.ExpiresAt));
            return refreshed;
        }

        public Task<bool> ValidateSessionAsync(TidalTokens session)
        {
            return Task.FromResult(session is not null && !session.IsExpired);
        }
    }

    private sealed class ShortLivedAuthService
        : IStreamingTokenAuthenticationService<TidalTokens, TidalCredentials>
    {
        public int AuthenticateCalls { get; private set; }

        public Task<TidalTokens> AuthenticateAsync(TidalCredentials credentials)
        {
            AuthenticateCalls++;
            TidalTokens tokens = new(
                $"short-access-{AuthenticateCalls}",
                $"short-refresh-{AuthenticateCalls}",
                "Bearer",
                DateTime.UtcNow.AddMinutes(1),
                $"session-{AuthenticateCalls}",
                "US",
                "user-1");
            return Task.FromResult(tokens);
        }

        public Task<bool> ValidateSessionAsync(TidalTokens session)
        {
            return Task.FromResult(session is not null && !session.IsExpired);
        }
    }

    private sealed class RefreshableTidalAuth : ITidalAuth, IStreamingTokenProvider
    {
        private TidalTokens current = new(
            "old-access",
            "refresh-token",
            "Bearer",
            DateTime.UtcNow.AddHours(1),
            "session-old",
            "US",
            "user-1");

        public bool IsAuthenticated => true;

        public bool SupportsRefresh => true;

        public string ServiceName => "Tidal";

        private int providerRefreshCalls;
        private int clearAuthenticationCacheCalls;

        public int ProviderRefreshCalls => Volatile.Read(ref this.providerRefreshCalls);
        public int ClearAuthenticationCacheCalls => Volatile.Read(ref this.clearAuthenticationCacheCalls);

        public TimeSpan RefreshDelay { get; init; }

        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
        {
            return Task.FromResult(new TidalAuthUrl("https://auth", "verifier", "state", string.Empty));
        }

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
        {
            return Task.FromResult(this.current);
        }

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
        {
            this.current = this.current with
            {
                AccessToken = $"new-access-{ProviderRefreshCalls}",
                RefreshToken = $"new-refresh-{ProviderRefreshCalls}",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            return Task.FromResult(this.current);
        }

        public Task<TidalTokens> GetValidTokensAsync()
        {
            return Task.FromResult(this.current);
        }

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
        {
            return TidalCallbackResult.Success("code", "state");
        }

        public Task<string> GetAccessTokenAsync()
        {
            return Task.FromResult(this.current.AccessToken);
        }

        public async Task<string> RefreshTokenAsync()
        {
            int refreshCall = Interlocked.Increment(ref this.providerRefreshCalls);
            if (RefreshDelay > TimeSpan.Zero)
            {
                await Task.Delay(RefreshDelay);
            }

            this.current = this.current with
            {
                AccessToken = $"new-access-{refreshCall}",
                RefreshToken = $"new-refresh-{refreshCall}",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            return this.current.AccessToken;
        }

        public Task<bool> ValidateTokenAsync(string token)
        {
            return Task.FromResult(string.Equals(token, this.current.AccessToken, StringComparison.Ordinal));
        }

        public DateTime? GetTokenExpiration(string token)
        {
            return string.Equals(token, this.current.AccessToken, StringComparison.Ordinal)
                ? this.current.ExpiresAt
                : null;
        }

        public void ClearAuthenticationCache()
        {
            Interlocked.Increment(ref this.clearAuthenticationCacheCalls);
        }
    }
}
