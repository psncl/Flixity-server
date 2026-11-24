using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Common.Api;

namespace server.Features.Movies.Tests;

public class CityMovieFinderTests
{
    [Fact]
    public async Task KeepsSuggestionOrderAndBlurbs_EvenIfLookupsFinishOutOfOrder()
    {
        var client = new FakeClient(async (title, _) =>
        {
            await Task.Delay(title == "M0" ? 100 : 1);
            return Movie(title);
        });

        var found = await Find(client, 4);

        found.Partial.Should().BeFalse();
        found.Movies.Select(f => f.Movie.Title).Should().Equal("M0", "M1", "M2", "M3");
        found.Movies.Select(f => f.Blurb).Should().Equal("B0", "B1", "B2", "B3");
    }

    [Fact]
    public async Task StopsAfterTenMovies()
    {
        var client = new FakeClient((title, _) => Task.FromResult<MovieInfo?>(Movie(title)));

        var found = await Find(client, 15);

        found.Movies.Select(f => f.Movie.Title).Should().Equal(Enumerable.Range(0, 10).Select(i => $"M{i}"));
        client.Titles.Should().HaveCount(10);
    }

    [Fact]
    public async Task LooksUpMore_WhenSomeAreNotFound()
    {
        var client = new FakeClient((title, _) =>
            Task.FromResult(title is "M0" or "M3" ? null : Movie(title)));

        var found = await Find(client, 15);

        found.Partial.Should().BeFalse();
        found.Movies.Select(f => f.Movie.Title).Should()
            .Equal("M1", "M2", "M4", "M5", "M6", "M7", "M8", "M9", "M10", "M11");
    }

    [Fact]
    public async Task IgnoresSuggestionsAfterTheFifteenth()
    {
        var client = new FakeClient((_, _) => Task.FromResult<MovieInfo?>(null));

        await Find(client, 20);

        client.Titles.Should().HaveCount(15);
    }

    [Fact]
    public async Task Duplicates_AreReturnedOnceAtFirstPosition_AndNotCounted()
    {
        var client = new FakeClient((title, _) =>
            Task.FromResult<MovieInfo?>(title == "M6" ? Movie("M1") : Movie(title)));

        var found = await Find(client, 15);

        found.Movies.Select(f => f.Movie.Title).Should()
            .Equal("M0", "M1", "M2", "M3", "M4", "M5", "M7", "M8", "M9", "M10");
        found.Movies[1].Blurb.Should().Be("B1");
    }

    [Fact]
    public async Task OneFailure_IsPartial_AndOthersContinue()
    {
        var client = new FakeClient((title, _) => title == "M2"
            ? throw new MovieInfoUnavailableException("down")
            : Task.FromResult<MovieInfo?>(Movie(title)));

        var found = await Find(client, 8);

        found.Partial.Should().BeTrue();
        found.Movies.Select(f => f.Movie.Title).Should().Equal("M0", "M1", "M3", "M4", "M5", "M6", "M7");
    }

    [Fact]
    public async Task Refusal_StartsNoFurtherLookups()
    {
        var client = new FakeClient((title, _) => title == "M2"
            ? throw new MovieInfoUnavailableException("quota", refused: true)
            : Task.FromResult<MovieInfo?>(Movie(title)));

        var found = await Find(client, 15);

        found.Partial.Should().BeTrue();
        found.Movies.Select(f => f.Movie.Title).Should().Equal("M0", "M1");
        client.Titles.Should().Equal("M0", "M1", "M2");
    }

    [Fact]
    public async Task BudgetUsedUp_KeepsFinishedLookups_AndIsPartial()
    {
        var client = new FakeClient(async (title, token) =>
        {
            if (title == "M1") await Task.Delay(Timeout.Infinite, token);
            return Movie(title);
        });

        var found = await Find(client, 15, TimeSpan.FromMilliseconds(100));

        found.Partial.Should().BeTrue();
        found.Movies.Select(f => f.Movie.Title).Should().Equal("M0", "M2", "M3", "M4");
        client.Titles.Should().HaveCount(5);
    }

    [Fact]
    public async Task FailureAfterTheTenthMovie_IsNotPartial()
    {
        // M10 is the tenth movie. M11 is in the same batch, but can't change the result.
        var client = new FakeClient((title, _) => title switch
        {
            "M0" => Task.FromResult<MovieInfo?>(null),
            "M11" => throw new MovieInfoUnavailableException("down"),
            _ => Task.FromResult<MovieInfo?>(Movie(title))
        });

        var found = await Find(client, 15);

        found.Partial.Should().BeFalse();
        found.Movies.Should().HaveCount(10);
        found.Movies[^1].Movie.Title.Should().Be("M10");
    }

    [Fact]
    public async Task BudgetUsedUpAsTheLastLookupSucceeds_IsNotPartial()
    {
        var client = new FakeClient(async (title, _) =>
        {
            await Task.Delay(150, CancellationToken.None);
            return Movie(title);
        });

        var found = await Find(client, 5, TimeSpan.FromMilliseconds(30));

        found.Partial.Should().BeFalse();
        found.Movies.Should().HaveCount(5);
    }

    [Fact]
    public async Task Refusal_LetsRunningLookupsFinish()
    {
        var client = new FakeClient(async (title, token) =>
        {
            if (title == "M1") throw new MovieInfoUnavailableException("quota", refused: true);
            await Task.Delay(50, token);
            return Movie(title);
        });

        var found = await Find(client, 15);

        found.Partial.Should().BeTrue();
        found.Movies.Select(f => f.Movie.Title).Should().Equal("M0");
        client.Titles.Should().Equal("M0", "M1");
    }

    [Fact]
    public async Task CancelledByCaller_Throws()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var client = new FakeClient(async (title, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Movie(title);
        });

        var act = () => Finder(client).FindAsync(Suggestions(1), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task NoSuggestions_IsCompleteAndEmpty()
    {
        var found = await Find(new FakeClient((_, _) => throw new InvalidOperationException()), 0);

        found.Partial.Should().BeFalse();
        found.Movies.Should().BeEmpty();
    }

    [Fact]
    public void DefaultBudget_Is30Seconds()
    {
        new CityMovieFinderOptions().Budget.Should().Be(TimeSpan.FromSeconds(30));
    }

    private static MovieInfo Movie(string title) => new($"tt-{title}", title, 2000, null, null, null);

    private static IEnumerable<MovieSuggestion> Suggestions(int count) =>
        Enumerable.Range(0, count).Select(i => new MovieSuggestion($"M{i}", 2000, $"B{i}"));

    private static Task<FoundMovies> Find(IMovieInfoClient client, int suggestions, TimeSpan? budget = null) =>
        Finder(client, budget).FindAsync(Suggestions(suggestions));

    private static CityMovieFinder Finder(IMovieInfoClient client, TimeSpan? budget = null) => new(client,
        Options.Create(new CityMovieFinderOptions { Budget = budget ?? TimeSpan.FromSeconds(30) }),
        NullLogger<CityMovieFinder>.Instance);

    private class FakeClient(Func<string, CancellationToken, Task<MovieInfo?>> find) : IMovieInfoClient
    {
        public List<string> Titles { get; } = [];

        public Task<MovieInfo?> FindMovieAsync(string title, int year, CancellationToken cancellationToken = default)
        {
            lock (Titles) Titles.Add(title);
            return find(title, cancellationToken);
        }
    }
}
