using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Common.Api;
using UnitTests;

namespace server.Features.Movies.Tests;

public class MovieSiteIdWikidataClientTests
{
    [Fact]
    public async Task ReturnsAllSiteIds()
    {
        var body = Bindings(Row("tt0137523", rt: "m/fight_club", mc: "movie/fight-club", lb: "fight-club", mubi: "918"));

        var ids = await Find(body, "tt0137523");

        ids.Should().Equal(new Dictionary<string, MovieSiteIds>
        {
            ["tt0137523"] = new("m/fight_club", "movie/fight-club", "fight-club", "918"),
        });
    }

    [Fact]
    public async Task MissingSiteIds_AreNull()
    {
        var ids = await Find(Bindings(Row("tt1", rt: "m/one")), "tt1");

        ids["tt1"].Should().Be(new MovieSiteIds("m/one", null, null, null));
    }

    [Fact]
    public async Task UnknownImdbIds_AreLeftOut()
    {
        var ids = await Find(Bindings(Row("tt1", mubi: "1")), "tt1", "tt2", "tt3");

        ids.Keys.Should().Equal("tt1");
    }

    [Fact]
    public async Task NoMatches_IsEmpty()
    {
        (await Find(Bindings(), "tt1")).Should().BeEmpty();
    }

    [Fact]
    public async Task UnrequestedImdbIds_AreIgnored()
    {
        var ids = await Find(Bindings(Row("tt1", rt: "m/one"), Row("tt999", rt: "m/other")), "tt1");

        ids.Keys.Should().Equal("tt1");
    }

    [Fact]
    public async Task SendsOneRequest_WithEveryImdbId()
    {
        var handler = Handler(Bindings());

        await Client(handler).FindSiteIdsAsync(["tt0137523", "tt0211915", "tt0137523"]);

        var query = handler.Requests.Should().ContainSingle().Which.Query;
        query.Should().StartWith("?query=");
        var sparql = Uri.UnescapeDataString(query["?query=".Length..]);
        sparql.Should().Contain("""VALUES ?imdb { "tt0137523" "tt0211915" }""");
        sparql.Should().Contain("wdt:P345").And.Contain("wdt:P1258").And.Contain("wdt:P1712")
            .And.Contain("wdt:P6127").And.Contain("wdt:P7299");
    }

    [Fact]
    public async Task SendsTheUserAgent_AndNoKey()
    {
        var handler = Handler(Bindings());

        await Client(handler).FindSiteIdsAsync(["tt1"]);

        handler.UserAgents.Should().Equal("TestApp/1.0 (test@example.test)");
        handler.Requests.Single().Query.Should().NotContainAny("key", "token");
    }

    [Theory]
    [InlineData("tt1\" } ?x ?y ?z { \"")] // An attempt to change the query
    [InlineData("nm0000093")] // A person, not a title
    [InlineData("")]
    [InlineData("TT1")]
    public async Task InvalidImdbIds_AreNotSent(string invalidId)
    {
        var handler = Handler(Bindings());

        await Client(handler).FindSiteIdsAsync([invalidId, "tt1"]);

        Uri.UnescapeDataString(handler.Requests.Single().Query).Should()
            .Contain("""VALUES ?imdb { "tt1" }""");
    }

    [Fact]
    public async Task NoValidImdbIds_SendsNoRequest()
    {
        var handler = Handler(Bindings());

        (await Client(handler).FindSiteIdsAsync(["nm1"])).Should().BeEmpty();
        (await Client(handler).FindSiteIdsAsync([])).Should().BeEmpty();

        handler.Requests.Should().BeEmpty();
    }

    /// <summary>A SPARQL JSON result row. Variables without a value are left out, as Wikidata does.</summary>
    private static Dictionary<string, object> Row(string imdb, string? rt = null, string? mc = null,
        string? lb = null, string? mubi = null)
    {
        var row = new Dictionary<string, object> { ["imdb"] = Literal(imdb) };
        if (rt is not null) row["rottenTomatoes"] = Literal(rt);
        if (mc is not null) row["metacritic"] = Literal(mc);
        if (lb is not null) row["letterboxd"] = Literal(lb);
        if (mubi is not null) row["mubi"] = Literal(mubi);
        return row;

        static object Literal(string value) => new { type = "literal", value };
    }

    private static string Bindings(params Dictionary<string, object>[] rows) => JsonSerializer.Serialize(new
    {
        head = new { vars = new[] { "imdb", "rottenTomatoes", "metacritic", "letterboxd", "mubi" } },
        results = new { bindings = rows },
    });

    private static FakeHttpHandler Handler(string body) => new(_ => FakeHttpHandler.Json(body));

    private static Task<IReadOnlyDictionary<string, MovieSiteIds>> Find(string body, params string[] imdbIds) =>
        Client(Handler(body)).FindSiteIdsAsync(imdbIds);

    private static MovieSiteIdWikidataClient Client(HttpMessageHandler handler, TimeSpan? attemptTimeout = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://wikidata.test/") },
            Options.Create(new WikidataOptions
            {
                UserAgent = "TestApp/1.0 (test@example.test)",
                AttemptTimeout = attemptTimeout ?? TimeSpan.FromSeconds(30),
                RetryDelay = TimeSpan.Zero,
            }),
            NullLogger<MovieSiteIdWikidataClient>.Instance);

    // Retries

    private static readonly string FoundBody = Bindings(Row("tt1", rt: "m/one"));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ServerError_ThenSuccess_ReturnsIds(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(_ => Status(status), _ => Found());

        var ids = await Client(handler).FindSiteIdsAsync(["tt1"]);

        ids.Keys.Should().Equal("tt1");
        handler.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task NoConnection_ThenSuccess_ReturnsIds()
    {
        var handler = new ScriptedHandler(_ => throw new HttpRequestException("No connection"), _ => Found());

        (await Client(handler).FindSiteIdsAsync(["tt1"])).Keys.Should().Equal("tt1");
    }

    [Fact]
    public async Task TwoFailures_ReturnEmpty_WithoutAThirdAttempt()
    {
        var handler = new ScriptedHandler(_ => Status(HttpStatusCode.ServiceUnavailable),
            _ => Status(HttpStatusCode.ServiceUnavailable), _ => Found());

        (await Client(handler).FindSiteIdsAsync(["tt1"])).Should().BeEmpty();
        handler.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task Timeout_ThenSuccess_ReturnsIds()
    {
        var handler = new ScriptedHandler(Hang, _ => Found());

        var ids = await Client(handler, attemptTimeout: TimeSpan.FromMilliseconds(50)).FindSiteIdsAsync(["tt1"]);

        ids.Keys.Should().Equal("tt1");
        handler.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task TwoTimeouts_ReturnEmpty()
    {
        var handler = new ScriptedHandler(Hang, Hang);

        (await Client(handler, attemptTimeout: TimeSpan.FromMilliseconds(50)).FindSiteIdsAsync(["tt1"]))
            .Should().BeEmpty();
        handler.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task TimeoutCoversReadingTheBody()
    {
        // Headers arrive at once, but the body never does.
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StreamContent(new HangingStream()) }, _ => Found());

        var ids = await Client(handler, attemptTimeout: TimeSpan.FromMilliseconds(50)).FindSiteIdsAsync(["tt1"]);

        ids.Keys.Should().Equal("tt1");
    }

    [Fact]
    public async Task RateLimited_WithShortRetryAfter_IsRetried()
    {
        var handler = new ScriptedHandler(_ => RateLimited(TimeSpan.Zero), _ => Found());

        (await Client(handler).FindSiteIdsAsync(["tt1"])).Keys.Should().Equal("tt1");
        handler.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task RateLimited_WithRetryAfterAsAPastDate_IsRetried()
    {
        var handler = new ScriptedHandler(_ => RateLimited(DateTimeOffset.UtcNow.AddMinutes(-1)), _ => Found());

        (await Client(handler).FindSiteIdsAsync(["tt1"])).Keys.Should().Equal("tt1");
    }

    [Theory]
    [InlineData(6)] // Just over the 5-second limit
    [InlineData(60)]
    public async Task RateLimited_WithLongRetryAfter_IsNotRetried(int seconds)
    {
        var handler = new ScriptedHandler(_ => RateLimited(TimeSpan.FromSeconds(seconds)), _ => Found());

        (await Client(handler).FindSiteIdsAsync(["tt1"])).Should().BeEmpty();
        handler.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task RateLimited_WithoutRetryAfter_IsNotRetried()
    {
        var handler = new ScriptedHandler(_ => Status(HttpStatusCode.TooManyRequests), _ => Found());

        (await Client(handler).FindSiteIdsAsync(["tt1"])).Should().BeEmpty();
        handler.Attempts.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)] // Wikidata's answer to a missing User-Agent
    public async Task ClientError_IsNotRetried(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(_ => Status(status), _ => Found());

        (await Client(handler).FindSiteIdsAsync(["tt1"])).Should().BeEmpty();
        handler.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task InvalidJson_ReturnsEmpty_WithoutRetry()
    {
        var handler = new ScriptedHandler(_ => FakeHttpHandler.Json("<html>oops</html>"), _ => Found());

        (await Client(handler).FindSiteIdsAsync(["tt1"])).Should().BeEmpty();
        handler.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task CallerCancelling_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new ScriptedHandler(token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Found();
        }, _ => Found());

        var act = () => Client(handler).FindSiteIdsAsync(["tt1"], cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.Attempts.Should().Be(1);
    }

    [Fact]
    public void Defaults_MatchTheDesign()
    {
        var options = new WikidataOptions();

        options.AttemptTimeout.Should().Be(TimeSpan.FromSeconds(10));
        options.MaxRetryAfter.Should().Be(TimeSpan.FromSeconds(5));
    }

    private static HttpResponseMessage Found() => FakeHttpHandler.Json(FoundBody);

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private static HttpResponseMessage RateLimited(TimeSpan retryAfter) => new(HttpStatusCode.TooManyRequests)
        { Headers = { RetryAfter = new RetryConditionHeaderValue(retryAfter) } };

    private static HttpResponseMessage RateLimited(DateTimeOffset retryAfter) => new(HttpStatusCode.TooManyRequests)
        { Headers = { RetryAfter = new RetryConditionHeaderValue(retryAfter) } };

    private static HttpResponseMessage Hang(CancellationToken token)
    {
        // Blocks until the attempt's timeout cancels it, then throws as HttpClient does.
        token.WaitHandle.WaitOne();
        token.ThrowIfCancellationRequested();
        throw new UnreachableException();
    }

    /// <summary>Answers each attempt with the next response in the script.</summary>
    private class ScriptedHandler(params Func<CancellationToken, HttpResponseMessage>[] script) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var respond = script[Attempts++];
            return await Task.Run(() => respond(cancellationToken), CancellationToken.None);
        }
    }

    /// <summary>A response body that never arrives, until the read is cancelled.</summary>
    private class HangingStream : Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
