using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;

namespace Tidalarr.Infrastructure.Storage;

/// <summary>
/// Token storage that refuses to write/delete when no durable storage location is configured.
/// </summary>
/// <remarks>
/// In plugin/Docker hosts, default paths can resolve to read-only locations or ephemeral temp directories.
/// Using this store prevents silent persistence to unexpected locations when <c>ConfigPath</c> is missing.
/// </remarks>
public sealed class FailOnIOTokenStore : ITokenStorage
{
    private const string ErrorMessage =
        "Token storage unavailable: ConfigPath is not configured. Set ConfigPath in the indexer/download client settings to enable token persistence.";

    public Task SaveTokensAsync(TidalTokens tokens)
    {
        throw new InvalidOperationException(ErrorMessage);
    }

    public Task<TidalTokens?> LoadTokensAsync()
    {
        return Task.FromResult<TidalTokens?>(null);
    }

    public Task DeleteTokensAsync()
    {
        throw new InvalidOperationException(ErrorMessage);
    }
}

