using System.Net;
using System.Text;

namespace UnitTests;

/// <summary>Answers HTTP requests without a network, and records them.</summary>
public class FakeHttpHandler(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    /// <summary>The User-Agent of each request, in the same order as <see cref="Requests"/>.</summary>
    public List<string> UserAgents { get; } = [];

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        UserAgents.Add(request.Headers.UserAgent.ToString());
        return Task.FromResult(respond(request.RequestUri!));
    }
}
