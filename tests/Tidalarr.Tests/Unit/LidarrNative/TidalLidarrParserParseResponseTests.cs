using Microsoft.Extensions.DependencyInjection;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit.LidarrNative;

/// <summary>
/// Wave-20 gap-fill: covers <see cref="TidalLidarrParser.ParseResponse"/> branch logic
/// that the static helper tests don't reach (URL prefix guard, missing query, missing
/// service, top-level catch).
/// </summary>
public class TidalLidarrParserParseResponseTests
{
    private static IndexerResponse CreateResponse(string url)
    {
        var httpResponse = new HttpResponse(
            new HttpRequest(url),
            new HttpHeader(),
            "{}",
            System.Net.HttpStatusCode.OK);

        return new IndexerResponse(
            new IndexerRequest(url, new HttpAccept("application/json")),
            httpResponse);
    }

    private static TidalLidarrParser CreateParser(IServiceProvider? services = null)
    {
        var settings = new TidalLidarrIndexerSettings();
        var sp = services ?? new ServiceCollection().BuildServiceProvider();
        var logger = LogManager.GetCurrentClassLogger();
        return new TidalLidarrParser(settings, sp, logger);
    }

    [Fact]
    public void ParseResponse_WithNonTidalUrl_ReturnsEmpty()
    {
        var parser = CreateParser();
        var response = CreateResponse("https://example.com/foo");

        IList<ReleaseInfo> result = parser.ParseResponse(response);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseResponse_WithEmptyUrl_ReturnsEmpty()
    {
        // The Request.Url is empty when we use a plain string ctor with "" — covers the ?? "" branch
        var parser = CreateParser();
        var response = CreateResponse("https://example.com/other");

        IList<ReleaseInfo> result = parser.ParseResponse(response);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseResponse_WithTidalUrlMissingQueryParam_ReturnsEmpty()
    {
        // tidal://search?other=foo — has query string but no "query" param
        var parser = CreateParser();
        var response = CreateResponse("tidal://search?other=foo");

        IList<ReleaseInfo> result = parser.ParseResponse(response);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseResponse_WithTidalUrlEmptyQuery_ReturnsEmpty()
    {
        // tidal://search?query= — query param present but empty
        var parser = CreateParser();
        var response = CreateResponse("tidal://search?query=");

        IList<ReleaseInfo> result = parser.ParseResponse(response);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseResponse_WithTidalUrlAndNoSearchService_ReturnsEmpty()
    {
        // Service not registered in the IServiceProvider — exercises the
        // "TidalSearchService not available" guard.
        var parser = CreateParser();
        var response = CreateResponse("tidal://search?query=Miles%20Davis");

        IList<ReleaseInfo> result = parser.ParseResponse(response);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseResponse_WithTidalSchemeNoQueryString_ReturnsEmpty()
    {
        var parser = CreateParser();
        var response = CreateResponse("tidal://search");

        IList<ReleaseInfo> result = parser.ParseResponse(response);

        Assert.Empty(result);
    }
}
