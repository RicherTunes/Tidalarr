using System.Linq;
using System.Threading.Tasks;
using Tidalarr.Core.Exceptions;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// The per-download AsyncLocal collector that bridges a permanent per-track stream restriction (observed
/// deep in the stream provider, where the album id is not in scope) up to the download client (which holds
/// the album id and decides suppression). Mirrors the codebase's existing
/// <c>DownloadTelemetryContext</c> AsyncLocal pattern.
/// </summary>
public sealed class TidalTerminalRestrictionScopeTests
{
    [Fact]
    public void Record_PermanentReason_WithinScope_IsCollected()
    {
        using (TidalTerminalRestrictionScope.Begin())
        {
            TidalTerminalRestrictionScope.Record("track-1", TidalStreamUnavailableReason.RightsRemoved);

            var terminals = TidalTerminalRestrictionScope.Snapshot();
            Assert.Single(terminals);
            Assert.Equal("track-1", terminals[0].TrackId);
            Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, terminals[0].Reason);
        }
    }

    [Fact]
    public void Record_TransientReason_IsIgnored()
    {
        using (TidalTerminalRestrictionScope.Begin())
        {
            TidalTerminalRestrictionScope.Record("track-1", TidalStreamUnavailableReason.Forbidden);
            TidalTerminalRestrictionScope.Record("track-2", TidalStreamUnavailableReason.NotReady);
            TidalTerminalRestrictionScope.Record("track-3", TidalStreamUnavailableReason.Unknown);

            Assert.Empty(TidalTerminalRestrictionScope.Snapshot());
        }
    }

    [Fact]
    public void Record_OutsideScope_IsNoOp_AndSnapshotIsEmpty()
    {
        // No active scope (e.g. a stand-alone quality probe): recording must not throw and there is
        // nothing to collect.
        TidalTerminalRestrictionScope.Record("track-1", TidalStreamUnavailableReason.RightsRemoved);

        Assert.Empty(TidalTerminalRestrictionScope.Snapshot());
    }

    [Fact]
    public async Task Record_FromChildTasks_FlowsBackToParentScope()
    {
        // The orchestrator downloads tracks concurrently (Task.WhenAll). The AsyncLocal value is set
        // before the child tasks fork, so the shared collector reference flows down and child writes
        // are visible to the parent after the join.
        using (TidalTerminalRestrictionScope.Begin())
        {
            await Task.WhenAll(
                Task.Run(() => TidalTerminalRestrictionScope.Record("t1", TidalStreamUnavailableReason.RightsRemoved)),
                Task.Run(() => TidalTerminalRestrictionScope.Record("t2", TidalStreamUnavailableReason.RightsRemoved)));

            var trackIds = TidalTerminalRestrictionScope.Snapshot().Select(t => t.TrackId).OrderBy(x => x).ToList();
            Assert.Equal(new[] { "t1", "t2" }, trackIds);
        }
    }

    [Fact]
    public void Scopes_AreIsolated_AndRestoredOnDispose()
    {
        using (TidalTerminalRestrictionScope.Begin())
        {
            TidalTerminalRestrictionScope.Record("outer", TidalStreamUnavailableReason.RightsRemoved);

            using (TidalTerminalRestrictionScope.Begin())
            {
                Assert.Empty(TidalTerminalRestrictionScope.Snapshot());
                TidalTerminalRestrictionScope.Record("inner", TidalStreamUnavailableReason.RightsRemoved);
                Assert.Single(TidalTerminalRestrictionScope.Snapshot());
            }

            // Inner scope disposed → outer restored with only its own observation.
            var terminals = TidalTerminalRestrictionScope.Snapshot();
            Assert.Single(terminals);
            Assert.Equal("outer", terminals[0].TrackId);
        }

        Assert.Empty(TidalTerminalRestrictionScope.Snapshot());
    }
}
