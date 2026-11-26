using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using server.Common.Api;
using UnitTests;

namespace server.Features.Movies.Tests;

public class MovieInfoOmdbApiClientTests
{
    private const string NotFound = """{"Response":"False","Error":"Movie not found!"}""";

    [Fact]
    public async Task ReturnsMetadata()
    {
        var movie = await Find("Amélie", 2002, Movie("Amélie", "2002"));

        movie.Should().Be(new MovieInfo("tt1", "Amélie", 2002, "Director", "Plot", "https://poster.test/1.jpg"));
    }

    [Fact]
    public async Task SendsKeyTypeTitleAndYear()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(Movie("Amélie", "2002")));

        await Client(handler).FindMovieAsync("Amélie", 2002);

        handler.Requests.Should().ContainSingle()
            .Which.Query.Should().Be("?apikey=k%26y&type=movie&t=Am%C3%A9lie&y=2002");
    }

    [Theory]
    [InlineData("Fast & Furious", "t=Fast%20%26%20Furious&y=2009")]
    [InlineData("#Alive", "t=%23Alive&y=2009")]
    public async Task EncodesTheCompleteTitle(string title, string expectedQuery)
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(Movie(title, "2009")));

        (await Client(handler).FindMovieAsync(title, 2009))!.Title.Should().Be(title);

        handler.Requests.Single().Query.Should().EndWith(expectedQuery);
    }

    [Fact]
    public async Task MissingMetadata_IsNull()
    {
        var movie = await Find("M", 2000, Movie("M", "2000", missing: "N/A"));

        movie.Should().Be(new MovieInfo("tt1", "M", 2000, null, null, null));
    }

    [Theory]
    [InlineData("2000–2003", true)]
    [InlineData("2000–", true)]
    [InlineData("N/A", false)]
    [InlineData("", false)]
    public async Task ParsesYearSafely(string year, bool found)
    {
        var movie = await Find("M", 2000, Movie("M", year));

        (movie is not null).Should().Be(found);
    }

    [Theory]
    [InlineData("Amélie", 2001, "Amélie", "2002", true)] // Year differs by one
    [InlineData("Amélie", 2003, "Amélie", "2002", true)]
    [InlineData("Amélie", 2004, "Amélie", "2002", false)]
    [InlineData("Amelie", 2001, "Amelie", "2017", false)] // Year too far away
    [InlineData("AMELIE!", 2002, "Amélie", "2002", true)] // Case, accents and punctuation
    [InlineData("Spider-Man: No Way Home", 2021, "Spider Man No Way Home", "2021", true)]
    [InlineData("Quantum of Solace", 2008, "Quantum of Solace: Director Marc Forster", "2009", false)]
    [InlineData("!!!", 2000, "???", "2000", false)]
    [InlineData("千と千尋", 2001, "千と千尋", "2001", true)]
    [InlineData("Møte", 2000, "Mote", "2000", true)] // Letters that are not a letter plus an accent
    [InlineData("Łódź", 2000, "Lodz", "2000", true)]
    [InlineData("Straße", 2000, "Strasse", "2000", true)]
    [InlineData("Æon Flux", 2005, "Aeon Flux", "2005", true)]
    [InlineData("か", 2000, "が", "2000", false)] // Marks that change the letter
    [InlineData("कि", 2000, "की", "2000", false)]
    public async Task VerifiesTitleAndYear(string title, int year, string foundTitle, string foundYear, bool accepted)
    {
        var movie = await Find(title, year, Movie(foundTitle, foundYear));

        (movie is not null).Should().Be(accepted);
        if (accepted) movie!.Year.Should().Be(year);
    }

    [Fact]
    public async Task ResultThatIsNotAMovie_IsDropped()
    {
        (await Find("M", 2000, Movie("M", "2000", type: "series"))).Should().BeNull();
    }

    [Fact]
    public async Task NotFoundWithYear_IsLookedUpOnceWithoutYear()
    {
        var handler = new FakeHttpHandler(uri =>
            FakeHttpHandler.Json(uri.Query.Contains("&y=") ? NotFound : Movie("Amélie", "2002")));

        (await Client(handler).FindMovieAsync("Amélie", 2001))!.Year.Should().Be(2001, "the requested year is kept");

        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task WrongMovieWithYear_IsLookedUpOnceWithoutYear()
    {
        var handler = new FakeHttpHandler(uri =>
            FakeHttpHandler.Json(uri.Query.Contains("&y=") ? Movie("Other", "2001") : Movie("Amélie", "2002")));

        (await Client(handler).FindMovieAsync("Amélie", 2001))!.Title.Should().Be("Amélie");
    }

    [Fact]
    public async Task NeverFound_IsNullAfterTwoLookups()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(NotFound));

        (await Client(handler).FindMovieAsync("M", 2000)).Should().BeNull();

        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task TemporaryFailure_ThenSuccess_IsFound()
    {
        var attempts = 0;
        var handler = new FakeHttpHandler(_ => ++attempts switch
        {
            1 => throw new HttpRequestException("No connection"),
            2 => new HttpResponseMessage(HttpStatusCode.BadGateway),
            _ => FakeHttpHandler.Json(Movie("M", "2000"))
        });

        (await Client(handler).FindMovieAsync("M", 2000)).Should().NotBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, 3, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 3, false)]
    [InlineData(HttpStatusCode.Unauthorized, 1, true)]
    [InlineData(HttpStatusCode.Forbidden, 1, false)]
    [InlineData(HttpStatusCode.TooManyRequests, 1, false)]
    public async Task HttpError_Throws(HttpStatusCode status, int expectedRequests, bool refused)
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(
            """{"Response":"False","Error":"Request limit reached!"}""", status));

        var act = () => Client(handler).FindMovieAsync("M", 2000);

        (await act.Should().ThrowAsync<MovieInfoUnavailableException>()).Which.Refused.Should().Be(refused);
        handler.Requests.Should().HaveCount(expectedRequests);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"Response":"False","Error":"Error getting data."}""")]
    public async Task UnexpectedBody_Throws(string body)
    {
        var act = () => Find("M", 2000, body);

        (await act.Should().ThrowAsync<MovieInfoUnavailableException>()).Which.Refused.Should().BeFalse();
    }

    [Fact]
    public async Task Timeout_IsRetriedThenThrows()
    {
        var handler = new HangingHandler();

        var act = () => Client(handler, TimeSpan.FromMilliseconds(30)).FindMovieAsync("M", 2000);

        await act.Should().ThrowAsync<MovieInfoUnavailableException>();
        handler.Requests.Should().Be(3);
    }

    [Fact]
    public async Task CancelledByCaller_IsNotRetried()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var handler = new HangingHandler();

        var act = () => Client(handler).FindMovieAsync("M", 2000, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task ReturnsAllRatings()
    {
        var body = WithRatings("8.8", "67", ("Internet Movie Database", "8.8/10"),
            ("Metacritic", "67/100"));

        var movie = await Find("M", 2000, body);

        movie!.ImdbRating.Should().Be(8.8m);
        movie.Metascore.Should().Be(67);
    }

    [Fact]
    public async Task MissingImdbRating_IsNull()
    {
        var movie = await Find("M", 2000, WithRatings("N/A", "67"));

        movie!.ImdbRating.Should().BeNull();
        movie.Metascore.Should().Be(67);
    }

    [Fact]
    public async Task MissingMetascore_IsNull()
    {
        var movie = await Find("M", 2000, WithRatings("8.8", "N/A"));

        movie!.Metascore.Should().BeNull();
        movie.ImdbRating.Should().Be(8.8m);
    }

    [Fact]
    public async Task NoRatingsAtAll_AreNull()
    {
        // The plain Movie body has no imdbRating, Metascore or Ratings fields.
        var movie = await Find("M", 2000, Movie("M", "2000"));

        movie!.ImdbRating.Should().BeNull();
        movie.Metascore.Should().BeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("10.5")] // Above the scale
    [InlineData("-1")]
    [InlineData("8,8")] // Decimal comma is not OMDb's format
    public async Task UnreadableImdbRating_IsNull(string value)
    {
        var movie = await Find("M", 2000, WithRatings(value, "67"));

        movie.Should().NotBeNull();
        movie!.ImdbRating.Should().BeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("101")]
    [InlineData("-5")]
    [InlineData("67.5")]
    public async Task UnreadableMetascore_IsNull(string value)
    {
        var movie = await Find("M", 2000, WithRatings("8.8", value));

        movie.Should().NotBeNull();
        movie!.Metascore.Should().BeNull();
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("10", "100")]
    public async Task RatingsAtTheEndsOfTheScale_AreKept(string imdb, string metascore)
    {
        var movie = await Find("M", 2000, WithRatings(imdb, metascore));

        movie!.ImdbRating.Should().Be(decimal.Parse(imdb));
        movie.Metascore.Should().Be(int.Parse(metascore));
    }

    [Fact]
    public void DefaultRetries_AreTwo()
    {
        new OmdbOptions().RetryDelays.Should().Equal(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1));
    }

    private static string Movie(string title, string year, string type = "movie", string? missing = null) =>
        JsonSerializer.Serialize(new
        {
            Title = title, Year = year, Type = type, imdbID = "tt1", Response = "True",
            Director = missing ?? "Director", Plot = missing ?? "Plot", Poster = missing ?? "https://poster.test/1.jpg"
        });

    /// <summary>A movie "M" from 2000, with OMDb's three rating fields.</summary>
    private static string WithRatings(string imdbRating, string metascore,
        params (string Source, string? Value)[] ratings) =>
        JsonSerializer.Serialize(new
        {
            Title = "M", Year = "2000", Type = "movie", imdbID = "tt1", Response = "True",
            imdbRating, Metascore = metascore,
            Ratings = ratings.Select(rating => new { rating.Source, rating.Value })
        });

    private static Task<MovieInfo?> Find(string title, int year, string body) =>
        Client(new FakeHttpHandler(_ => FakeHttpHandler.Json(body))).FindMovieAsync(title, year);

    private static MovieInfoOmdbApiClient Client(HttpMessageHandler handler, TimeSpan? timeout = null) => new(
        new HttpClient(handler)
        {
            BaseAddress = new Uri("https://omdb.test/"), Timeout = timeout ?? TimeSpan.FromSeconds(30)
        },
        Options.Create(new OmdbOptions { ApiKey = "k&y", RetryDelays = [TimeSpan.Zero, TimeSpan.Zero] }));

    private class HangingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }
}
