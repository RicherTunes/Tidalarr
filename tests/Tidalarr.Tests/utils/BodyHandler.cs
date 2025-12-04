using System.Net;
using System.Text;

namespace tests_Tidalarr_Tests_Utils;

public class BodyHandler(string body, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
{
    private readonly string _body = body;
    private readonly HttpStatusCode _code = code;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(this._code)
        {
            Content = new StringContent(this._body, Encoding.UTF8, "application/json")
        });
    }
}




