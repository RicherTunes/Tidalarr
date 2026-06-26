using Lidarr.Plugin.Common.Services.Intelligence;
using Lidarr.Plugin.Common.TestKit.Compliance;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Tidalarr's adoption of the cross-plugin <c>search-query-sanitizer</c> parity axis. Runs the full
/// shared tricky-character corpus through the same canonical <see cref="SearchQuerySanitizer"/> the
/// Tidal indexer now uses (<c>TidalLidarrRequestGenerator</c> → <see cref="SearchQuerySanitizer.BuildPlan"/>),
/// asserting the universal invariants every plugin must hold so a future Common change can't silently
/// regress the contract on the Tidal side.
/// </summary>
public sealed class TidalSearchQuerySanitizerParityTests : SearchQuerySanitizerParityTestBase
{
    protected override SanitizedQuery SanitizeViaPlugin(string? raw) => SearchQuerySanitizer.Sanitize(raw);
}
