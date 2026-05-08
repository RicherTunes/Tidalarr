using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Interfaces;
using Lidarr.Plugin.Common.TestKit.Compliance;

namespace Tidalarr.Infrastructure.Storage;

/// <summary>
/// Token store that refuses to persist or clear tokens when no durable storage location
/// is configured.
/// </summary>
/// <remarks>
/// In plugin/Docker hosts, default paths can resolve to read-only locations or ephemeral temp
/// directories. Using this store prevents silent persistence to unexpected locations when
/// <c>ConfigPath</c> is missing. Implements common's <see cref="ITokenStore{TSession}"/> so the
/// runtime can inject either this fail-fast variant or a real encrypted file-backed store.
/// </remarks>
/// <typeparam name="TSession">Session representation type.</typeparam>
[ParityAllowedTokenStore("Deliberate fail-fast no-op for plugin start-up scenarios where ConfigPath is missing — not a fork of common's storage layer.")]
public sealed class FailOnIOTokenStore<TSession> : ITokenStore<TSession>
    where TSession : class
{
    private const string ErrorMessage =
        "Token storage unavailable: ConfigPath is not configured. Set ConfigPath in the indexer/download client settings to enable token persistence.";

    /// <inheritdoc />
    public Task<TokenEnvelope<TSession>?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<TokenEnvelope<TSession>?>(null);
    }

    /// <inheritdoc />
    public Task SaveAsync(TokenEnvelope<TSession> envelope, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(ErrorMessage);
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(ErrorMessage);
    }
}
