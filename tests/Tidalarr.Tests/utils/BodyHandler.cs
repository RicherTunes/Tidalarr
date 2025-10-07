using System.Net;
using System.Text;

namespace tests_Tidalarr_Tests_Utils;

public class BodyHandler : HttpMessageHandler
{
    private readonly string _body;
    private readonly HttpStatusCode _code;
    public BodyHandler(string body, HttpStatusCode code = HttpStatusCode.OK) { _body = body; _code = code; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_code)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        });
    }
}




