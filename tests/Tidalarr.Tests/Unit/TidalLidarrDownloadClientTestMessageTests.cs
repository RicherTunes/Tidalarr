using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Regression guards for TidalLidarrDownloadClient's Test() failure message.
///
/// Previously the outer catch in Test() emitted "Test failed: {ex.Message}"
/// which leaked CLR-flavoured exception text — users couldn't tell auth from
/// network from rate-limit at a glance. The new BuildTestFailureMessage
/// helper delegates to common's HttpExceptionClassifier so users see
/// categorised user-readable hints with the CLR type names suppressed.
/// </summary>
public sealed class TidalLidarrDownloadClientTestMessageTests
{
    [Fact]
    public void BuildTestFailureMessage_401_HintMentionsCredentials()
    {
        var ex = new HttpRequestException("Unauthorized", inner: null, statusCode: HttpStatusCode.Unauthorized);
        var message = TidalLidarrDownloadClient.BuildTestFailureMessage(ex);
        Assert.Contains("credentials", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTestFailureMessage_429_HintMentionsRateLimit()
    {
        var ex = new HttpRequestException("Too Many Requests", inner: null, statusCode: HttpStatusCode.TooManyRequests);
        var message = TidalLidarrDownloadClient.BuildTestFailureMessage(ex);
        Assert.Contains("rate", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTestFailureMessage_5xx_HintMentionsServerOrTryAgain()
    {
        var ex = new HttpRequestException("Service Unavailable", inner: null, statusCode: HttpStatusCode.ServiceUnavailable);
        var message = TidalLidarrDownloadClient.BuildTestFailureMessage(ex);
        Assert.True(
            message.Contains("server", StringComparison.OrdinalIgnoreCase)
            || message.Contains("try again", StringComparison.OrdinalIgnoreCase),
            $"expected server-error hint, got: {message}");
    }

    [Fact]
    public void BuildTestFailureMessage_TaskCanceled_HintMentionsTimeout()
    {
        var ex = new TaskCanceledException("A task was canceled.");
        var message = TidalLidarrDownloadClient.BuildTestFailureMessage(ex);
        Assert.Contains("timed out", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTestFailureMessage_SocketException_HintMentionsNetwork()
    {
        var ex = new SocketException((int)SocketError.HostNotFound);
        var message = TidalLidarrDownloadClient.BuildTestFailureMessage(ex);
        Assert.Contains("network", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildTestFailureMessage_DoesNotLeakClrTypeOrExceptionWord()
    {
        // Regression guard — the whole point of the swap is to prevent
        // ex.GetType().Name from appearing in user-visible text.
        var inputs = new Exception[]
        {
            new HttpRequestException("x", null, HttpStatusCode.Unauthorized),
            new HttpRequestException("x", null, HttpStatusCode.TooManyRequests),
            new HttpRequestException("x", null, HttpStatusCode.InternalServerError),
            new TaskCanceledException("timeout"),
            new SocketException(11001),
            new InvalidOperationException("opaque"),
            new IOException("boom")
        };
        foreach (var ex in inputs)
        {
            var message = TidalLidarrDownloadClient.BuildTestFailureMessage(ex);
            Assert.False(message.Contains("System.", StringComparison.Ordinal),
                $"leaked CLR namespace: '{message}'");
            Assert.False(message.Contains("Exception", StringComparison.Ordinal),
                $"leaked 'Exception': '{message}'");
        }
    }

    [Fact]
    public void BuildTestFailureMessage_MentionsLogsForOperatorDeepDive()
    {
        var ex = new InvalidOperationException("opaque");
        var message = TidalLidarrDownloadClient.BuildTestFailureMessage(ex);
        Assert.Contains("log", message, StringComparison.OrdinalIgnoreCase);
    }
}
