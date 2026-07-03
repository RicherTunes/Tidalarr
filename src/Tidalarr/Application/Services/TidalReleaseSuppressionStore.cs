using System;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.HostBridge;
using Tidalarr.Core.Constants;
using Tidalarr.Core.Exceptions;

namespace Tidalarr.Application.Services;

/// <summary>
/// Tidal policy adapter over Common's durable <see cref="TerminalReleaseSuppressionStore"/>. Common owns
/// persistence, TTL, bounds, normalization, and the synchronous parser lookup; this type owns only the
/// Tidal-specific terminal-reason classification (a release is suppressed on a permanent stream restriction,
/// keyed by album id). Mirrors qobuz's <c>RestrictedReleaseSuppressionStore</c>.
/// </summary>
public sealed class TidalReleaseSuppressionStore : ITidalReleaseSuppressionStore
{
    public const int DefaultMaxEntries = TerminalReleaseSuppressionStore.DefaultMaxEntries;

    public static readonly TimeSpan DefaultTtl = TerminalReleaseSuppressionStore.DefaultTtl;

    private readonly ITerminalReleaseSuppressionStore _inner;

    public TidalReleaseSuppressionStore(
        string filePath,
        TimeSpan? ttl = null,
        int? maxEntries = null,
        TimeProvider? clock = null,
        TimeSpan? refreshInterval = null)
        : this(new TerminalReleaseSuppressionStore(
            filePath,
            TidalConstants.PluginName,
            ttl,
            maxEntries,
            clock,
            refreshInterval))
    {
    }

    internal TidalReleaseSuppressionStore(ITerminalReleaseSuppressionStore inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public static TidalReleaseSuppressionStore Shared => _shared.Value;

    public int Count => _inner is TerminalReleaseSuppressionStore store ? store.Count : 0;

    private static readonly Lazy<TidalReleaseSuppressionStore> _shared = new(
        () => new TidalReleaseSuppressionStore(
            TerminalReleaseSuppressionStore.ForPlugin(TidalConstants.PluginName)),
        isThreadSafe: true);

    public bool IsSuppressed(string albumId) => _inner.IsSuppressed(albumId);

    public Task SuppressAsync(
        string albumId,
        string trackId,
        TidalStreamUnavailableReason reason,
        CancellationToken cancellationToken = default)
    {
        // Defense in depth: even if a caller reaches this with a transient reason, only a permanent one is
        // ever persisted. The record's key is the album id; the track id and reason are stored for
        // diagnostics only.
        if (!ShouldSuppress(reason))
        {
            return Task.CompletedTask;
        }

        return _inner.SuppressAsync(albumId, trackId, reason.ToString(), cancellationToken);
    }

    public static bool ShouldSuppress(TidalStreamUnavailableReason reason) => reason.IsPermanent();

    public Task<bool> ClearAsync(string albumId, CancellationToken cancellationToken = default)
        => _inner.ClearAsync(albumId, cancellationToken);
}

/// <summary>
/// Tidal-specific suppression surface consumed by the parser (read) and download client (write).
/// </summary>
public interface ITidalReleaseSuppressionStore
{
    bool IsSuppressed(string albumId);

    Task SuppressAsync(string albumId, string trackId, TidalStreamUnavailableReason reason, CancellationToken cancellationToken = default);

    Task<bool> ClearAsync(string albumId, CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op store (never suppresses). Default so any caller that does not opt into suppression behaves
/// identically to before the feature.
/// </summary>
public sealed class NullTidalReleaseSuppressionStore : ITidalReleaseSuppressionStore
{
    public static readonly NullTidalReleaseSuppressionStore Instance = new();

    private NullTidalReleaseSuppressionStore()
    {
    }

    public bool IsSuppressed(string albumId) => false;

    public Task SuppressAsync(string albumId, string trackId, TidalStreamUnavailableReason reason, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> ClearAsync(string albumId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
