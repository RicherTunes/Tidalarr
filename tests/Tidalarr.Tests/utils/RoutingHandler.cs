using System.Net;
using System.Text;

namespace tests_Tidalarr_Tests_Utils;

public class RoutingHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> match, Func<HttpRequestMessage, HttpResponseMessage> handler)> _routes = [];

    public RoutingHandler Map(Func<HttpRequestMessage, bool> predicate, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        this._routes.Add((predicate, responder));
        return this;
    }

    public RoutingHandler MapPath(string pathStartsWith, string content, HttpStatusCode code = HttpStatusCode.OK, string contentType = "application/json")
    {
        return Map(r => r.RequestUri != null && r.RequestUri.AbsolutePath.StartsWith(pathStartsWith, StringComparison.OrdinalIgnoreCase),
                   _ => new HttpResponseMessage(code) { Content = new StringContent(content, Encoding.UTF8, contentType) });
    }

    public RoutingHandler MapAny(string content, HttpStatusCode code = HttpStatusCode.OK, string contentType = "application/json")
    {
        return Map(_ => true, _ => new HttpResponseMessage(code) { Content = new StringContent(content, Encoding.UTF8, contentType) });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        foreach ((Func<HttpRequestMessage, bool> match, Func<HttpRequestMessage, HttpResponseMessage> handler) in this._routes)
        {
            if (match(request))
            {
                return Task.FromResult(handler(request));
            }
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}




