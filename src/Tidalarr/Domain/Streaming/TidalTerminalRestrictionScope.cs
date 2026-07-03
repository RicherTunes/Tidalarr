using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Tidalarr.Core.Exceptions;

namespace Tidalarr.Domain.Streaming;

/// <summary>
/// A single observed permanent (terminal) per-track stream restriction.
/// </summary>
public sealed class TidalTerminalRestriction(string trackId, TidalStreamUnavailableReason reason)
{
    public string TrackId { get; } = trackId;
    public TidalStreamUnavailableReason Reason { get; } = reason;
}

/// <summary>
/// Ambient, per-download collector that bridges a permanent per-track stream restriction — observed deep
/// in the stream provider, where the album id is not in scope — up to the download client, which holds the
/// album id and owns the suppression decision.
///
/// <para>Uses the same <see cref="AsyncLocal{T}"/> pattern as Common's <c>DownloadTelemetryContext</c>:
/// the download client calls <see cref="Begin"/> before starting the album download, the value (a mutable
/// thread-safe bag) flows down into every per-track task (including the orchestrator's concurrent
/// <c>Task.WhenAll</c> path), and child writes are visible to the parent after the join. Recording is a
/// no-op when no scope is active (e.g. a stand-alone quality probe), and only permanent reasons are
/// collected — a transient failure is never recorded and therefore can never suppress.</para>
/// </summary>
public static class TidalTerminalRestrictionScope
{
    private static readonly AsyncLocal<ConcurrentBag<TidalTerminalRestriction>?> Current = new();

    /// <summary>
    /// Begins a collection scope for one album download. Dispose restores the prior scope.
    /// </summary>
    public static System.IDisposable Begin()
    {
        var prior = Current.Value;
        Current.Value = new ConcurrentBag<TidalTerminalRestriction>();
        return new Scope(prior);
    }

    /// <summary>
    /// Records a terminal restriction if a scope is active and the reason is PERMANENT. Transient reasons
    /// and calls outside any scope are silently ignored — the safety bias means only an explicit permanent
    /// signal is ever collected.
    /// </summary>
    public static void Record(string trackId, TidalStreamUnavailableReason reason)
    {
        if (!reason.IsPermanent())
        {
            return;
        }

        Current.Value?.Add(new TidalTerminalRestriction(trackId, reason));
    }

    /// <summary>
    /// Snapshot of the terminal restrictions collected in the current scope, or empty when no scope is
    /// active.
    /// </summary>
    public static IReadOnlyList<TidalTerminalRestriction> Snapshot()
    {
        var current = Current.Value;
        return current is null ? System.Array.Empty<TidalTerminalRestriction>() : current.ToArray();
    }

    private sealed class Scope(ConcurrentBag<TidalTerminalRestriction>? prior) : System.IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Current.Value = prior;
            }
        }
    }
}
