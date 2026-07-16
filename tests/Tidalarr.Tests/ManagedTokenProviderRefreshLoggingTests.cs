using System.Collections.Concurrent;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

/// <summary>
/// T-5 (silent token-seam catch): <c>ManagedTokenProvider.RefreshTokenCoreAsync</c> used to swallow
/// every refresh failure with a bare <c>catch { return string.Empty; }</c> (plus a silent inner
/// catch on the post-OAuth re-prime), leaving zero log signal when auth silently degraded. These
/// tests pin that a failed refresh logs a WARNING (redacted — no token material) and the best-effort
/// re-prime failure logs at DEBUG, without changing the return-value contract.
/// </summary>
[Trait("Category", "Unit")]
public class ManagedTokenProviderRefreshLoggingTests
{
    private const string SecretFragment = "SECRETTOKEN123";

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshFails_ReturnsEmptyAndLogsWarning()
    {
        CapturingLoggerProvider capture = new();
        using ServiceProvider provider = BuildProvider(capture, new ThrowingAuthService());

        IStreamingTokenProvider tokenProvider = provider.GetRequiredService<IStreamingTokenProvider>();
        string result = await tokenProvider.RefreshTokenAsync();

        // Contract unchanged: failure still surfaces as an empty token, never a throw.
        Assert.Equal(string.Empty, result);

        LogEntry? warning = capture.Entries.FirstOrDefault(e =>
            e.Category.Contains(nameof(ManagedTokenProvider), StringComparison.Ordinal) &&
            e.Level == LogLevel.Warning);
        Assert.True(warning is not null,
            "a failed token refresh must log a warning from ManagedTokenProvider instead of being swallowed silently");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRefreshFails_WarningContainsNoTokenMaterial()
    {
        CapturingLoggerProvider capture = new();
        using ServiceProvider provider = BuildProvider(capture, new ThrowingAuthService());

        IStreamingTokenProvider tokenProvider = provider.GetRequiredService<IStreamingTokenProvider>();
        _ = await tokenProvider.RefreshTokenAsync();

        List<LogEntry> providerEntries = capture.Entries
            .Where(e => e.Category.Contains(nameof(ManagedTokenProvider), StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(providerEntries);
        Assert.All(providerEntries, entry =>
            Assert.DoesNotContain(SecretFragment, entry.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenOAuthRefreshSucceedsButRePrimeFails_ReturnsTokenAndLogsRePrimeFailure()
    {
        CapturingLoggerProvider capture = new();
        using ServiceProvider provider = BuildProvider(
            capture,
            new ThrowingAuthService(),
            tidalAuth: new RefreshCapableAuth());

        IStreamingTokenProvider tokenProvider = provider.GetRequiredService<IStreamingTokenProvider>();
        string result = await tokenProvider.RefreshTokenAsync();

        // The OAuth-refreshed token is still returned — the re-prime is best-effort.
        Assert.Equal("fresh-access-token", result);

        LogEntry? rePrime = capture.Entries.FirstOrDefault(e =>
            e.Category.Contains(nameof(ManagedTokenProvider), StringComparison.Ordinal) &&
            e.Message.Contains("re-prime", StringComparison.OrdinalIgnoreCase));
        Assert.True(rePrime is not null,
            "a failed post-OAuth re-prime must leave a log signal instead of being swallowed silently");
        Assert.DoesNotContain(SecretFragment, rePrime!.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(
        CapturingLoggerProvider capture,
        IStreamingTokenAuthenticationService<TidalTokens, TidalCredentials> authService,
        ITidalAuth? tidalAuth = null)
    {
        ServiceCollection services = new();
        _ = services.AddLogging(builder =>
        {
            _ = builder.AddProvider(capture);
            _ = builder.SetMinimumLevel(LogLevel.Trace);
        });
        _ = services.AddSingleton(new TidalarrSettings
        {
            ConfigPath = Path.Combine(Path.GetTempPath(), "tidalarr-refresh-logging-tests"),
            RedirectUrl = "https://tidal.com/android/login/auth"
        });

        TidalModule.RegisterServices(services);

        _ = services.Replace(ServiceDescriptor.Singleton(authService));
        // Default: an ITidalAuth that is NOT an IStreamingTokenProvider, so RefreshTokenCoreAsync
        // takes the manager fallback path (which the throwing auth service fails).
        _ = services.Replace(ServiceDescriptor.Singleton(tidalAuth ?? new NonRefreshableAuth()));

        return services.BuildServiceProvider();
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> entries = new();

        public IReadOnlyCollection<LogEntry> Entries => this.entries;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this.entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), exception));
        }
    }

    /// <summary>Fails every refresh with a message carrying token-shaped material (redaction probe).</summary>
    private sealed class ThrowingAuthService : IStreamingTokenAuthenticationService<TidalTokens, TidalCredentials>
    {
        public Task<TidalTokens> AuthenticateAsync(TidalCredentials credentials)
            => throw new InvalidOperationException($"Tidal refresh rejected: token={SecretFragment} is invalid");

        public Task<bool> ValidateSessionAsync(TidalTokens session) => Task.FromResult(false);
    }

    /// <summary>ITidalAuth that deliberately does NOT implement IStreamingTokenProvider.</summary>
    private sealed class NonRefreshableAuth : ITidalAuth
    {
        public bool IsAuthenticated => false;

        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
            => Task.FromResult(new TidalAuthUrl("https://auth", "verifier", "state", string.Empty));

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
            => throw new InvalidOperationException("not authenticated");

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
            => throw new InvalidOperationException("not authenticated");

        public Task<TidalTokens> GetValidTokensAsync()
            => throw new InvalidOperationException("not authenticated");

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
            => TidalCallbackResult.Success("code", "state");
    }

    /// <summary>
    /// ITidalAuth + IStreamingTokenProvider whose OAuth refresh succeeds — drives the inner
    /// (re-prime) catch when the manager's auth service throws.
    /// </summary>
    private sealed class RefreshCapableAuth : ITidalAuth, IStreamingTokenProvider
    {
        public bool IsAuthenticated => true;

        public bool SupportsRefresh => true;

        public string ServiceName => "Tidal";

        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
            => Task.FromResult(new TidalAuthUrl("https://auth", "verifier", "state", string.Empty));

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
            => Task.FromResult(Tokens());

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
            => Task.FromResult(Tokens());

        public Task<TidalTokens> GetValidTokensAsync()
            => Task.FromResult(Tokens());

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
            => TidalCallbackResult.Success("code", "state");

        public Task<string> GetAccessTokenAsync() => Task.FromResult("fresh-access-token");

        public Task<string> RefreshTokenAsync() => Task.FromResult("fresh-access-token");

        public Task<bool> ValidateTokenAsync(string token) => Task.FromResult(true);

        public DateTime? GetTokenExpiration(string token) => null;

        public void ClearAuthenticationCache()
        {
        }

        private static TidalTokens Tokens() => new(
            "fresh-access-token",
            "fresh-refresh-token",
            "Bearer",
            DateTime.UtcNow.AddHours(1),
            "session-1",
            "US",
            "user-1");
    }
}
