using System.Text.Json;
using FluentAssertions;
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

    private static MovieSiteIdWikidataClient Client(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://wikidata.test/") },
        Options.Create(new WikidataOptions { UserAgent = "TestApp/1.0 (test@example.test)" }));
}
