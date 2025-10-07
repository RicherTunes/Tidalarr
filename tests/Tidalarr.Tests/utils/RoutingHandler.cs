using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tests_Tidalarr_Tests_Utils;

public class RoutingHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> match, Func<HttpRequestMessage, HttpResponseMessage> handler)> _routes = new();

    public RoutingHandler Map(Func<HttpRequestMessage, bool> predicate, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _routes.Add((predicate, responder));
        return this;
    }

    public RoutingHandler MapPath(string pathStartsWith, string content, HttpStatusCode code = HttpStatusCode.OK, string contentType = "application/json")
        => Map(r => r.RequestUri != null && r.RequestUri.AbsolutePath.StartsWith(pathStartsWith, StringComparison.OrdinalIgnoreCase),
               _ => new HttpResponseMessage(code) { Content = new StringContent(content, Encoding.UTF8, contentType) });

    public RoutingHandler MapAny(string content, HttpStatusCode code = HttpStatusCode.OK, string contentType = "application/json")
        => Map(_ => true, _ => new HttpResponseMessage(code) { Content = new StringContent(content, Encoding.UTF8, contentType) });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        foreach (var (match, handler) in _routes)
        {
            if (match(request))
            {
                return Task.FromResult(handler(request));
            }
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}




