using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Tidalarr.Core.Exceptions;
using Tidalarr.Domain.Streaming;

namespace Tidalarr.Application.Services;

/// <summary>
/// Host-free decision for recording terminal-release suppression after a FAILED album download: if a
/// permanent (terminal) per-track restriction was observed, record the album id in the store. Best-effort —
/// a store failure is logged and swallowed so it can never mask the original download failure.
///
/// <para>Extracted out of the host-coupled <c>TidalLidarrDownloadClient</c> (which extends
/// <c>DownloadClientBase</c> and therefore drags in Lidarr.Core) so this reliability-critical decision — the
/// suppress-on-terminal / don't-suppress-otherwise gate — is unit-testable under the <c>ExcludeHostBridge=true</c>
/// hermetic CI build, not only in the full local suite.</para>
/// </summary>
public static class TidalTerminalSuppressionRecorder
{
    public static async Task TryRecordAsync(
        ITidalReleaseSuppressionStore store,
        string albumId,
        IReadOnlyList<TidalTerminalRestriction> terminalRestrictions,
        Logger? logger)
    {
        var terminal = terminalRestrictions?.FirstOrDefault(t => t.Reason.IsPermanent());
        if (terminal is null || string.IsNullOrWhiteSpace(albumId))
        {
            return;
        }

        try
        {
            await store.SuppressAsync(albumId, terminal.TrackId ?? string.Empty, terminal.Reason, CancellationToken.None).ConfigureAwait(false);
            logger?.Warn(
                "Suppressed Tidal album {0} from future automatic searches after terminal track restriction ({1}: {2})",
                albumId, terminal.TrackId, terminal.Reason);
        }
        catch (Exception storeException)
        {
            logger?.Warn(
                storeException,
                "Failed to record terminal Tidal release suppression for album {0}; preserving original download failure",
                albumId);
        }
    }
}
