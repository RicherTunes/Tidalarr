using Tidalarr.Core.Exceptions;
using Tidalarr.Domain.Streaming;
using Xunit;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Classification correctness is the whole game for terminal-release suppression: mis-classifying a
/// TRANSIENT failure as PERMANENT permanently hides a recoverable album (a false negative — the album is
/// never re-grabbed automatically). These tests pin the conservative "only an unambiguous
/// rights-removed signal is permanent; everything else is transient" policy and the safety bias
/// (ambiguous/empty/unknown => transient).
/// </summary>
public class TidalStreamRestrictionClassifierTests
{
    // ---- PERMANENT: the track's playable asset is gone from the catalog (rights removed / delisted). ----
    // A 404 from tracks/{id}/playbackinfopostpaywall means Tidal has no deliverable asset for a track
    // that IS listed on the album — region gating returns 401 sub-status codes, not 404, so a 404 here is
    // not a geo artifact and no quality tier can satisfy the grab.

    [Fact]
    public void Classify_Http404_IsRightsRemoved_AndPermanent()
    {
        var reason = TidalStreamRestrictionClassifier.Classify(404, subStatus: null, userMessage: null);

        Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, reason);
        Assert.True(reason.IsPermanent());
    }

    [Fact]
    public void Classify_Http404_SubStatus2001_IsRightsRemoved_AndPermanent()
    {
        var reason = TidalStreamRestrictionClassifier.Classify(404, subStatus: 2001, userMessage: "The requested resource could not be found");

        Assert.Equal(TidalStreamUnavailableReason.RightsRemoved, reason);
        Assert.True(reason.IsPermanent());
    }

    // ---- TRANSIENT: never suppressed. Each of these can succeed on a later grab. ----

    [Theory]
    [InlineData(401, null)]        // auth needs refresh
    [InlineData(401, 4006)]        // token expired
    [InlineData(401, 4005)]        // asset not ready for playback (still processing)
    [InlineData(403, null)]        // forbidden — region / tier / rights that may change (qobuz precedent: geo is NOT permanent)
    [InlineData(403, 4010)]        // region / streaming-not-available
    [InlineData(429, null)]        // rate limited
    [InlineData(500, null)]        // server error
    [InlineData(503, null)]        // service unavailable
    [InlineData(418, null)]        // unrecognized status
    public void Classify_NonRightsRemovedStatuses_AreTransient(int httpStatus, int? subStatus)
    {
        var reason = TidalStreamRestrictionClassifier.Classify(httpStatus, subStatus, userMessage: null);

        Assert.False(reason.IsPermanent(),
            $"HTTP {httpStatus}/sub {subStatus?.ToString() ?? "null"} must never be treated as permanent (would risk a false negative).");
    }

    [Fact]
    public void Classify_NotReadySubStatus_IsNotReady_AndTransient()
    {
        var reason = TidalStreamRestrictionClassifier.Classify(401, subStatus: 4005, userMessage: "Asset is not ready for playback");

        Assert.Equal(TidalStreamUnavailableReason.NotReady, reason);
        Assert.False(reason.IsPermanent());
    }

    // ---- SAFETY BIAS: empty / malformed / unknown defaults to transient. ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(200)]   // a 2xx should never have reached the classifier, but if it does, do not suppress
    public void Classify_UnknownOrEmptyStatus_DefaultsToUnknownTransient(int httpStatus)
    {
        var reason = TidalStreamRestrictionClassifier.Classify(httpStatus, subStatus: null, userMessage: "");

        Assert.False(reason.IsPermanent());
    }

    [Fact]
    public void Classify_404_WithNotReadySubStatus_PrefersTransientNotReady()
    {
        // Safety bias: an explicit "still processing" sub-status must win even alongside a 404, so a
        // transient not-ready state can never be upgraded to a permanent rights-removed suppression.
        var reason = TidalStreamRestrictionClassifier.Classify(404, subStatus: 4005, userMessage: "Asset is not ready for playback");

        Assert.Equal(TidalStreamUnavailableReason.NotReady, reason);
        Assert.False(reason.IsPermanent());
    }

    [Fact]
    public void IsPermanent_OnlyRightsRemovedIsTrue()
    {
        foreach (TidalStreamUnavailableReason reason in System.Enum.GetValues(typeof(TidalStreamUnavailableReason)))
        {
            bool expected = reason == TidalStreamUnavailableReason.RightsRemoved;
            Assert.Equal(expected, reason.IsPermanent());
        }
    }
}
