using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using server.Common.Api;
using server.Common.Database;
using server.Features.Movies;

namespace IntegrationTests;

/// <summary>External services are faked behind our interfaces. Their HTTP and JSON handling has unit tests.</summary>
[Collection(DatabaseCollection.Name)]
public class SearchCityMoviesTests : IAsyncLifetime
{
    private const string IpHeader = "Test-IP";
    private const int CityId = 900_000_001;
    private const int OtherCityId = 900_000_002;

    private readonly DatabaseFixture _database;
    private readonly ICityGeocoder _geocoder = Substitute.For<ICityGeocoder>();
    private readonly IMovieInfoLlmClient _llm = Substitute.For<IMovieInfoLlmClient>();
    private readonly IMovieInfoClient _movies = Substitute.For<IMovieInfoClient>();
    private readonly IMovieSiteIdClient _siteIds = Substitute.For<IMovieSiteIdClient>();
    private readonly SaveBarrier _saveBarrier = new();
    private readonly WebApplicationFactory<Program> _api;

    public SearchCityMoviesTests(DatabaseFixture database)
    {
        _database = database;
        _api = new ApiFactory(database).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(_geocoder);
            services.AddSingleton(_llm);
            services.AddSingleton(_movies);
            services.AddSingleton(_siteIds);
            services.AddSingleton<IStartupFilter, TestIpFilter>();
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(_saveBarrier));
        }));

        _geocoder.GetByIdAsync(default).ReturnsForAnyArgs(call => City(call.Arg<int>()));
        LlmSuggests("A", "B");
        _movies.FindMovieAsync(default!, default).ReturnsForAnyArgs(call => Movie(call.Arg<string>()));
        WikidataKnows();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    // Remove what the API saved, because other tests share the database.
    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
        await using var db = _database.CreateDbContext();
        await db.Cities.Where(city => city.GeoNamesId >= CityId).ExecuteDeleteAsync();
        await db.Movies.ExecuteDeleteAsync();
    }

    // First search

    [Fact]
    public async Task FirstSearch_ReturnsDocumentedResponse()
    {
        LlmSuggests("A");
        _movies.FindMovieAsync(default!, default)
            .ReturnsForAnyArgs(new MovieInfo("tt1", "A", 2000, null, null, null));

        var response = await SearchAsync();

        (await response.Content.ReadAsStringAsync()).Should().Be(
            """{"city":{"geoNamesId":900000001,"name":"Testville","region":null,"country":"Testland","countryCode":"TL"}""" +
            ""","partial":false,"movies":[{"imdbId":"tt1","title":"A","year":2000,"director":null""" +
            ""","posterUrl":null,"plot":null,"blurb":"Blurb A","links":[""" +
            """{"site":"imdb","url":"https://www.imdb.com/title/tt1/","rating":null,"isSearch":false},""" +
            """{"site":"letterboxd","url":"https://letterboxd.com/imdb/tt1/","rating":null,"isSearch":false},""" +
            """{"site":"rottenTomatoes","url":"https://www.rottentomatoes.com/search?search=A","rating":null,"isSearch":true},""" +
            """{"site":"metacritic","url":"https://www.metacritic.com/search/A/","rating":null,"isSearch":true},""" +
            """{"site":"mubi","url":"https://mubi.com/en/search/films?query=A","rating":null,"isSearch":true}""" +
            """]}]}""");
    }

    [Fact]
    public async Task NewCity_IsIdentifiedByGeocoder_AndSavedWithItsMovies()
    {
        await SearchOkAsync();

        await _llm.Received(1).GetMoviesFilmedInAsync(City(CityId), Arg.Any<CancellationToken>());
        await using var db = _database.CreateDbContext();
        var city = await db.Cities.SingleAsync(c => c.GeoNamesId == CityId);
        city.Name.Should().Be("Testville");
        city.IsFeatured.Should().BeFalse();
        city.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        (await db.FilmingLocations.Where(l => l.CityId == city.Id).OrderBy(l => l.Position)
            .Select(l => l.Movie.Title).ToListAsync()).Should().Equal("A", "B");
    }

    [Fact]
    public async Task StoredCity_IsNotLookedUpAgain()
    {
        await StoreCityAsync("Stored name");

        var result = await SearchOkAsync();

        result.City.Name.Should().Be("Stored name");
        _geocoder.ReceivedCalls().Should().BeEmpty();
        await using var db = _database.CreateDbContext();
        (await db.Cities.SingleAsync(c => c.GeoNamesId == CityId)).GeneratedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SharedMovie_IsStoredOnceUnchanged_WithABlurbPerCity()
    {
        LlmSuggests("A");
        await SearchOkAsync(CityId);

        _llm.GetMoviesFilmedInAsync(default!).ReturnsForAnyArgs([new MovieSuggestion("A", 2000, "Other blurb")]);
        _movies.FindMovieAsync(default!, default)
            .ReturnsForAnyArgs(Movie("A") with { Plot = "Changed plot" });
        var second = await SearchOkAsync(OtherCityId);

        second.Movies.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Plot = "Plot A", Blurb = "Other blurb" });
        (await SearchOkAsync(CityId)).Movies.Single().Blurb.Should().Be("Blurb A");
        await using var db = _database.CreateDbContext();
        (await db.Movies.CountAsync(movie => movie.ImdbId == "tt-A")).Should().Be(1);
    }

    // Ratings and site IDs

    [Fact]
    public async Task FirstSearch_SavesRatingsAndSiteIds()
    {
        LlmSuggests("A");
        _movies.FindMovieAsync(default!, default).ReturnsForAnyArgs(
            Movie("A") with { ImdbRating = 8.8m, RottenTomatoes = 79, Metascore = 67 });
        WikidataKnows(("tt-A", new MovieSiteIds("m/a", "movie/a", "a", "1")));

        await SearchOkAsync();

        await using var db = _database.CreateDbContext();
        (await db.Movies.SingleAsync(movie => movie.ImdbId == "tt-A")).Should().BeEquivalentTo(new
        {
            ImdbRating = 8.8m, RottenTomatoes = 79, Metascore = 67,
            RottenTomatoesId = "m/a", MetacriticId = "movie/a", LetterboxdId = "a", MubiId = "1",
        });
    }

    [Fact]
    public async Task FirstSearch_LooksUpAllNewMovies_InOneRequest()
    {
        await SearchOkAsync();

        await _siteIds.Received(1).FindSiteIdsAsync(
            Arg.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "tt-A", "tt-B" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MovieUnknownToWikidata_IsSavedWithoutSiteIds_OthersAreUnaffected()
    {
        WikidataKnows(("tt-A", new MovieSiteIds("m/a", null, null, null)));

        await SearchOkAsync();

        await using var db = _database.CreateDbContext();
        (await db.Movies.SingleAsync(movie => movie.ImdbId == "tt-A")).RottenTomatoesId.Should().Be("m/a");
        (await db.Movies.SingleAsync(movie => movie.ImdbId == "tt-B")).Should().BeEquivalentTo(new
            { RottenTomatoesId = (string?)null, MetacriticId = (string?)null, LetterboxdId = (string?)null,
              MubiId = (string?)null });
    }

    [Fact]
    public async Task SharedMovie_IsNotLookedUpAgain_AndKeepsItsRatingsAndSiteIds()
    {
        LlmSuggests("A");
        _movies.FindMovieAsync(default!, default).ReturnsForAnyArgs(Movie("A") with { ImdbRating = 7.0m });
        WikidataKnows(("tt-A", new MovieSiteIds("m/a", null, null, null)));
        await SearchOkAsync(CityId);
        _siteIds.ClearReceivedCalls();

        // The second City finds the same Movie and a new one, with different data from both sources.
        LlmSuggests("A", "B");
        _movies.FindMovieAsync(default!, default).ReturnsForAnyArgs(call =>
            Movie(call.Arg<string>()) with { ImdbRating = 9.9m });
        WikidataKnows(("tt-A", new MovieSiteIds("m/changed", null, null, null)),
            ("tt-B", new MovieSiteIds("m/b", null, null, null)));
        await SearchOkAsync(OtherCityId);

        await _siteIds.Received(1).FindSiteIdsAsync(
            Arg.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "tt-B" })),
            Arg.Any<CancellationToken>());
        await using var db = _database.CreateDbContext();
        (await db.Movies.SingleAsync(movie => movie.ImdbId == "tt-A")).Should().BeEquivalentTo(new
            { ImdbRating = 7.0m, RottenTomatoesId = "m/a" });
        (await db.Movies.SingleAsync(movie => movie.ImdbId == "tt-B")).RottenTomatoesId.Should().Be("m/b");
    }

    [Fact]
    public async Task SiteIdsUnavailable_ResultIsCompleteAndSaved_WithoutSiteIds()
    {
        // The client returns no IDs when Wikidata fails, rather than throwing.
        WikidataKnows();

        var result = await SearchOkAsync();

        result.Partial.Should().BeFalse();
        await using var db = _database.CreateDbContext();
        (await db.Cities.SingleAsync(c => c.GeoNamesId == CityId)).GeneratedAt.Should().NotBeNull();
        (await db.Movies.Where(movie => movie.ImdbId == "tt-A" || movie.ImdbId == "tt-B").ToListAsync())
            .Should().HaveCount(2).And.OnlyContain(movie => movie.RottenTomatoesId == null &&
                                                            movie.MetacriticId == null &&
                                                            movie.LetterboxdId == null && movie.MubiId == null);
    }

    // Links in the response

    [Fact]
    public async Task FirstSearch_ReturnsFiveLinksInOrder_WithRatings()
    {
        LlmSuggests("A");
        _movies.FindMovieAsync(default!, default).ReturnsForAnyArgs(
            Movie("A") with { ImdbRating = 8.8m, RottenTomatoes = 79, Metascore = 67 });
        WikidataKnows(("tt-A", new MovieSiteIds("m/a", "movie/a", "a", "1")));

        var result = await SearchOkAsync();

        result.Movies.Single().Links.Should().Equal(
            new MovieLink(MovieSite.Imdb, "https://www.imdb.com/title/tt-A/", "8.8/10", false),
            new MovieLink(MovieSite.Letterboxd, "https://letterboxd.com/film/a/", null, false),
            new MovieLink(MovieSite.RottenTomatoes, "https://www.rottentomatoes.com/m/a", "79%", false),
            new MovieLink(MovieSite.Metacritic, "https://www.metacritic.com/movie/a", "67/100", false),
            new MovieLink(MovieSite.Mubi, "https://mubi.com/films/1", null, false));
    }

    [Fact]
    public async Task SavedResult_ReturnsTheSameLinks_WithEverythingUnavailable()
    {
        _movies.FindMovieAsync(default!, default).ReturnsForAnyArgs(call =>
            Movie(call.Arg<string>()) with { ImdbRating = 7.5m, RottenTomatoes = 90, Metascore = 70 });
        WikidataKnows(("tt-A", new MovieSiteIds("m/a", null, "a", null)));
        var first = await SearchOkAsync();
        MakeEverythingUnavailable();
        WikidataKnows(("tt-A", new MovieSiteIds("m/changed", "movie/changed", "changed", "2")));
        _siteIds.ClearReceivedCalls();

        var second = await SearchOkAsync();

        second.Movies.Select(movie => movie.Links).Should().BeEquivalentTo(
            first.Movies.Select(movie => movie.Links), options => options.WithStrictOrdering());
        second.Movies.Should().HaveCount(2).And.OnlyContain(movie => movie.Links.Count == 5);
        _siteIds.ReceivedCalls().Should().BeEmpty("a saved result contacts no external service");
    }

    [Fact]
    public async Task PartialResult_HasLinks_FromSiteIdsAndStoredMovies()
    {
        // A is stored by an earlier search, with its own site IDs.
        LlmSuggests("A");
        WikidataKnows(("tt-A", new MovieSiteIds(null, null, null, "1")));
        await SearchOkAsync(OtherCityId);

        LlmSuggests("A", "B", "C");
        _movies.FindMovieAsync("C", default).ThrowsAsyncForAnyArgs(new MovieInfoUnavailableException("down"));
        _movies.FindMovieAsync("A", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Movie("A"));
        _movies.FindMovieAsync("B", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Movie("B"));
        WikidataKnows(("tt-A", new MovieSiteIds(null, null, null, "changed")),
            ("tt-B", new MovieSiteIds(null, null, null, "2")));

        var result = await SearchOkAsync();

        result.Partial.Should().BeTrue();
        result.Movies.Select(movie => movie.Links.Single(link => link.Site == MovieSite.Mubi).Url).Should()
            .Equal("https://mubi.com/films/1", "https://mubi.com/films/2");
    }

    // Cached searches

    [Fact]
    public async Task RepeatSearch_ReturnsTheSame_WithEverythingUnavailable()
    {
        LlmSuggests("C", "A", "B");
        var first = await SearchOkAsync();
        MakeEverythingUnavailable();

        var second = await SearchOkAsync();

        second.Should().BeEquivalentTo(first, options => options.WithStrictOrdering());
        second.Movies.Select(movie => movie.Title).Should().Equal("C", "A", "B");
    }

    [Fact]
    public async Task CompleteEmptyResult_IsCached()
    {
        LlmSuggests();
        (await SearchOkAsync()).Should().BeEquivalentTo(new { Partial = false, Movies = Array.Empty<object>() });
        MakeEverythingUnavailable();

        (await SearchOkAsync()).Movies.Should().BeEmpty();
    }

    [Fact]
    public async Task NoMovieSurvives_IsCachedAsEmpty()
    {
        _movies.FindMovieAsync(default!, default).ReturnsForAnyArgs((MovieInfo?)null);
        (await SearchOkAsync()).Partial.Should().BeFalse();
        MakeEverythingUnavailable();

        (await SearchOkAsync()).Movies.Should().BeEmpty();
    }

    // Failures

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("99999999999")]
    public async Task InvalidId_Is400_WithoutContactingServices(string id)
    {
        var response = await _api.CreateClient().PostAsync($"/cities/{id}/search", null);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest);
        _geocoder.ReceivedCalls().Should().BeEmpty();
        _llm.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownCity_Is404_WithoutAskingForMovies()
    {
        _geocoder.GetByIdAsync(default).ReturnsForAnyArgs((CityInfo?)null);

        await ShouldBeProblemAsync(await SearchAsync(), HttpStatusCode.NotFound);

        _llm.ReceivedCalls().Should().BeEmpty();
        await NothingShouldBeSavedAsync();
    }

    [Fact]
    public async Task GeocoderUnavailable_Is503()
    {
        _geocoder.GetByIdAsync(default).ThrowsAsyncForAnyArgs(new GeocoderUnavailableException("secret"));

        await ShouldBeProblemAsync(await SearchAsync(), HttpStatusCode.ServiceUnavailable);
        await NothingShouldBeSavedAsync();
    }

    [Fact]
    public async Task LlmUnavailable_Is503()
    {
        _llm.GetMoviesFilmedInAsync(default!).ThrowsAsyncForAnyArgs(new LlmUnavailableException("secret"));

        await ShouldBeProblemAsync(await SearchAsync(), HttpStatusCode.ServiceUnavailable);
        await NothingShouldBeSavedAsync();
    }

    [Fact]
    public async Task OneLookupFails_IsPartial_AndNextSearchGeneratesAgain()
    {
        _movies.FindMovieAsync("B", default).ThrowsAsyncForAnyArgs(new MovieInfoUnavailableException("down"));
        _movies.FindMovieAsync("A", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Movie("A"));

        var partial = await SearchOkAsync();

        partial.Partial.Should().BeTrue();
        partial.Movies.Select(movie => movie.Title).Should().Equal("A");
        partial.City.Name.Should().Be("Testville");
        await NothingShouldBeSavedAsync();

        _movies.FindMovieAsync("B", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Movie("B"));
        (await SearchOkAsync()).Should().BeEquivalentTo(new { Partial = false, Movies = new[] { new { Title = "A" }, new { Title = "B" } } });
    }

    [Fact]
    public async Task EveryLookupFails_IsPartialAndEmpty()
    {
        _movies.FindMovieAsync(default!, default).ThrowsAsyncForAnyArgs(new MovieInfoUnavailableException("down"));

        var result = await SearchOkAsync();

        result.Partial.Should().BeTrue();
        result.Movies.Should().BeEmpty();
        await NothingShouldBeSavedAsync();
    }

    // Simultaneous searches

    [Fact]
    public async Task SimultaneousSearchesOfOneCity_GenerateOnce_AndUseOnePermit()
    {
        var llmMayAnswer = new TaskCompletionSource();
        _llm.GetMoviesFilmedInAsync(default!).ReturnsForAnyArgs(async _ =>
        {
            await llmMayAnswer.Task;
            return (IReadOnlyList<MovieSuggestion>)[Suggestion("A")];
        });
        await UsePermitsAsync(UncachedSearchLimiter.Limit - 1);

        var searches = Enumerable.Range(0, 5).Select(_ => SearchOkAsync()).ToList();
        await Task.Delay(300); // Let the others reach the lock
        llmMayAnswer.SetResult();
        var results = await Task.WhenAll(searches);

        results.Should().AllBeEquivalentTo(results[0]);
        results[0].Movies.Should().ContainSingle();
        await _llm.ReceivedWithAnyArgs(1).GetMoviesFilmedInAsync(default!);
    }

    [Fact]
    public async Task SimultaneousSearchesOfTwoCities_CanAddTheSameMovie()
    {
        // Both searches have checked which Movies exist before either saves.
        _saveBarrier.WaitFor(2);

        var results = await Task.WhenAll(SearchOkAsync(CityId), SearchOkAsync(OtherCityId));

        results.Should().OnlyContain(result => result.Movies.Count == 2 && !result.Partial);
        await using var db = _database.CreateDbContext();
        (await db.Movies.CountAsync(movie => movie.ImdbId == "tt-A")).Should().Be(1);
        _saveBarrier.Saves.Should().Be(3, "one search must have tried again");
    }

    [Theory]
    [InlineData("failure", HttpStatusCode.ServiceUnavailable)]
    [InlineData("partial", HttpStatusCode.OK)]
    [InlineData("unknown city", HttpStatusCode.NotFound)]
    public async Task WaitingSearches_GetTheSameUnsavedOutcome_WithoutGeneratingAgain(string outcome,
        HttpStatusCode expected)
    {
        await UsePermitsAsync(UncachedSearchLimiter.Limit - 1);
        _geocoder.ClearReceivedCalls();
        var mayContinue = new TaskCompletionSource();
        _geocoder.GetByIdAsync(CityId, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await mayContinue.Task;
            return outcome == "unknown city" ? null : City(CityId);
        });
        if (outcome == "failure")
            _llm.GetMoviesFilmedInAsync(default!).ThrowsAsyncForAnyArgs(new LlmUnavailableException("down"));
        if (outcome == "partial")
            _movies.FindMovieAsync(default!, default).ReturnsForAnyArgs(call => call.Arg<string>() == "B"
                ? throw new MovieInfoUnavailableException("down")
                : Movie(call.Arg<string>()));

        var searches = Enumerable.Range(0, 4).Select(_ => SearchAsync()).ToList();
        await Task.Delay(300); // Let the others reach the lock
        mayContinue.SetResult();
        var responses = await Task.WhenAll(searches);

        // Only one permit was left, so the others can't have used any.
        responses.Should().OnlyContain(response => response.StatusCode == expected);
        _geocoder.ReceivedCalls().Should().ContainSingle();
        await NothingShouldBeSavedAsync();
        if (outcome != "partial") return;
        foreach (var response in responses)
            (await response.Content.ReadFromJsonAsync<SearchCityMoviesResponse>()).Should().BeEquivalentTo(
                new { Partial = true, Movies = new[] { new { Title = "A" } } });
    }

    [Fact]
    public async Task FirstClientLeaves_WaitingSearchGenerates()
    {
        var calls = 0;
        _llm.GetMoviesFilmedInAsync(default!).ReturnsForAnyArgs(async call =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            return (IReadOnlyList<MovieSuggestion>)[Suggestion("A")];
        });
        using var leave = new CancellationTokenSource();
        var first = _api.CreateClient().PostAsync($"/cities/{CityId}/search", null, leave.Token);
        await Task.Delay(300);
        var second = SearchOkAsync();
        await Task.Delay(300);

        await leave.CancelAsync();

        (await second.WaitAsync(TimeSpan.FromSeconds(10))).Movies.Should().ContainSingle();
        var act = () => first;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task NewMovies_AreInsertedInImdbIdOrder_SoThatSearchesCannotDeadlock()
    {
        LlmSuggests("C", "A", "B");

        (await SearchOkAsync()).Movies.Select(movie => movie.Title).Should().Equal("C", "A", "B");

        await using var db = _database.CreateDbContext();
        (await db.Movies.OrderBy(movie => movie.Id).Select(movie => movie.ImdbId).ToListAsync())
            .Should().Equal("tt-A", "tt-B", "tt-C");
    }

    [Fact]
    public async Task Deadlock_IsRetried()
    {
        // This is how EF Core reports errors that may go away.
        _saveBarrier.FailOnceWith(new InvalidOperationException("transient", new DbUpdateException("save",
            new PostgresException("deadlock", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected))));

        (await SearchOkAsync()).Movies.Should().HaveCount(2);
    }

    // Rate limit

    [Fact]
    public async Task LimitExceeded_Is429_WithoutContactingServices()
    {
        await UsePermitsAsync(UncachedSearchLimiter.Limit);
        _geocoder.ClearReceivedCalls();

        var response = await SearchAsync();

        await ShouldBeProblemAsync(response, HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.FromMinutes(50))
            .And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(60));
        _geocoder.ReceivedCalls().Should().BeEmpty();
        _llm.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task CachedSearches_AreNotCounted_AndWorkAtTheLimit()
    {
        await SearchOkAsync(CityId); // 1 permit
        for (var i = 0; i < UncachedSearchLimiter.Limit + 5; i++) await SearchOkAsync(CityId);

        await UsePermitsAsync(UncachedSearchLimiter.Limit - 1);

        (await SearchAsync(OtherCityId)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await SearchOkAsync(CityId)).Movies.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("unknown city")]
    [InlineData("failure")]
    [InlineData("partial")]
    public async Task UnsuccessfulSearches_AreCounted(string outcome)
    {
        if (outcome == "unknown city") _geocoder.GetByIdAsync(default).ReturnsForAnyArgs((CityInfo?)null);
        if (outcome == "failure")
            _llm.GetMoviesFilmedInAsync(default!).ThrowsAsyncForAnyArgs(new LlmUnavailableException("down"));
        if (outcome == "partial")
            _movies.FindMovieAsync(default!, default).ThrowsAsyncForAnyArgs(new MovieInfoUnavailableException("down"));

        for (var i = 0; i < UncachedSearchLimiter.Limit; i++)
            (await SearchAsync()).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        (await SearchAsync()).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task OtherEndpoints_WorkAtTheLimit()
    {
        await UsePermitsAsync(UncachedSearchLimiter.Limit);
        _geocoder.SearchAsync(default!, default!).ReturnsForAnyArgs([City(CityId)]);
        var client = _api.CreateClient();

        (await client.GetAsync("/cities/featured")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/cities?query=test")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OtherIpAddress_HasItsOwnLimit()
    {
        await UsePermitsAsync(UncachedSearchLimiter.Limit);

        (await SearchAsync(ip: "10.0.0.2")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SearchAsync(OtherCityId, ip: "::ffff:10.0.0.1")).StatusCode.Should()
            .Be(HttpStatusCode.TooManyRequests, "it is the same address as 10.0.0.1");
    }

    [Fact]
    public async Task Ipv6Network_SharesOneLimit()
    {
        _geocoder.GetByIdAsync(default).ReturnsForAnyArgs((CityInfo?)null);
        for (var i = 1; i <= UncachedSearchLimiter.Limit; i++) await SearchAsync(ip: $"2001:db8:1:1::{i:x}");

        (await SearchAsync(ip: "2001:db8:1:1:ffff::1")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await SearchAsync(ip: "2001:db8:1:2::1")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Limit_Is20PerHour()
    {
        UncachedSearchLimiter.Limit.Should().Be(20);
        UncachedSearchLimiter.Window.Should().Be(TimeSpan.FromMinutes(60));
    }

    // Retired endpoints

    [Fact]
    public async Task OldEndpoints_Are404()
    {
        var client = _api.CreateClient();

        (await client.GetAsync("/movies?title=a&year=2000")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/movies/search", new { location = "Paris" })).StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    // Helpers

    private static CityInfo City(int id) => new(id, "Testville", null, "Testland", "TL");

    private static MovieInfo Movie(string title) =>
        new($"tt-{title}", title, 2000, "Director", $"Plot {title}", "https://poster.test/1.jpg");

    private static MovieSuggestion Suggestion(string title) => new(title, 2000, $"Blurb {title}");

    private void LlmSuggests(params string[] titles) =>
        _llm.GetMoviesFilmedInAsync(default!).ReturnsForAnyArgs(titles.Select(Suggestion).ToList());

    /// <summary>Wikidata returns these site IDs, and nothing for any other IMDb ID.</summary>
    private void WikidataKnows(params (string ImdbId, MovieSiteIds Ids)[] known) =>
        _siteIds.FindSiteIdsAsync(default!, default).ReturnsForAnyArgs(call =>
        {
            // Null when NSubstitute runs the previous setup while this one replaces it.
            var asked = call.Arg<IReadOnlyCollection<string>?>() ?? [];
            return (IReadOnlyDictionary<string, MovieSiteIds>)known.Where(k => asked.Contains(k.ImdbId))
                .ToDictionary(k => k.ImdbId, k => k.Ids);
        });

    private void MakeEverythingUnavailable()
    {
        _geocoder.GetByIdAsync(default).ThrowsAsyncForAnyArgs(new GeocoderUnavailableException("down"));
        _llm.GetMoviesFilmedInAsync(default!).ThrowsAsyncForAnyArgs(new LlmUnavailableException("down"));
        _movies.FindMovieAsync(default!, default).ThrowsAsyncForAnyArgs(new MovieInfoUnavailableException("down"));
    }

    /// <summary>Uses up uncached searches with a City that doesn't exist, which saves nothing.</summary>
    private async Task UsePermitsAsync(int count)
    {
        const int unknownId = 900_000_099;
        _geocoder.GetByIdAsync(unknownId, Arg.Any<CancellationToken>()).Returns((CityInfo?)null);
        for (var i = 0; i < count; i++)
            (await SearchAsync(unknownId)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> SearchAsync(int id = CityId, string ip = "10.0.0.1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/cities/{id}/search");
        request.Headers.Add(IpHeader, ip);
        return await _api.CreateClient().SendAsync(request);
    }

    private async Task<SearchCityMoviesResponse> SearchOkAsync(int id = CityId)
    {
        var response = await SearchAsync(id);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<SearchCityMoviesResponse>())!;
    }

    private async Task StoreCityAsync(string name)
    {
        await using var db = _database.CreateDbContext();
        db.Cities.Add(new City { GeoNamesId = CityId, Name = name, Country = "Testland", CountryCode = "TL" });
        await db.SaveChangesAsync();
    }

    private async Task NothingShouldBeSavedAsync()
    {
        await using var db = _database.CreateDbContext();
        (await db.Cities.AnyAsync(city => city.GeoNamesId >= CityId)).Should().BeFalse();
        (await db.Movies.AnyAsync()).Should().BeFalse();
        (await db.FilmingLocations.AnyAsync()).Should().BeFalse();
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be((int)status);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("secret");
    }

    /// <summary>Requests in tests have no IP address, so take it from a header.</summary>
    private class TestIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continueRequest) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[IpHeader], out var address))
                    context.Connection.RemoteIpAddress = address;
                return continueRequest(context);
            });
            next(app);
        };
    }

    /// <summary>Holds back saves until a number of them are ready, so that they really happen at once.</summary>
    private class SaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _allReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Exception? _failure;
        private int _waitFor;
        private int _saves;

        public int Saves => _saves;

        public void WaitFor(int saves) => _waitFor = saves;

        public void FailOnceWith(Exception failure) => _failure = failure;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _failure, null) is { } failure) throw failure;
            if (Interlocked.Increment(ref _saves) >= _waitFor) _allReady.TrySetResult();
            await _allReady.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}
