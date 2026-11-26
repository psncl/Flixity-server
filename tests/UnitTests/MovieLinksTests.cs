using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using server.Common.Database;

namespace server.Features.Movies.Tests;

public class MovieLinksTests
{
    [Fact]
    public void FiveLinks_InOrder()
    {
        MovieLinks.For(FightClub()).Select(link => link.Site).Should().Equal(
            MovieSite.Imdb, MovieSite.Letterboxd, MovieSite.RottenTomatoes, MovieSite.Metacritic, MovieSite.Mubi);
    }

    [Fact]
    public void FiveLinks_InOrder_WithoutSiteIds()
    {
        MovieLinks.For(FightClub(withSiteIds: false)).Select(link => link.Site).Should().Equal(
            MovieSite.Imdb, MovieSite.Letterboxd, MovieSite.RottenTomatoes, MovieSite.Metacritic, MovieSite.Mubi);
    }

    [Fact]
    public void ImdbLink()
    {
        Link(FightClub(), MovieSite.Imdb).Should().Be(new MovieLink(MovieSite.Imdb,
            "https://www.imdb.com/title/tt0137523/", "8.8/10", IsSearch: false));
    }

    [Fact]
    public void KnownSiteIds_LinkDirectly()
    {
        var movie = FightClub();

        Link(movie, MovieSite.RottenTomatoes).Should().Be(new MovieLink(MovieSite.RottenTomatoes,
            "https://www.rottentomatoes.com/m/fight_club", "79%", IsSearch: false));
        Link(movie, MovieSite.Metacritic).Should().Be(new MovieLink(MovieSite.Metacritic,
            "https://www.metacritic.com/movie/fight-club", "67/100", IsSearch: false));
        Link(movie, MovieSite.Mubi).Should().Be(new MovieLink(MovieSite.Mubi,
            "https://mubi.com/films/918", null, IsSearch: false));
    }

    [Fact]
    public void LetterboxdIdKnown_LinksToTheFilm()
    {
        Link(FightClub(), MovieSite.Letterboxd).Should().Be(new MovieLink(MovieSite.Letterboxd,
            "https://letterboxd.com/film/fight-club/", null, IsSearch: false));
    }

    [Fact]
    public void LetterboxdIdUnknown_LinksByImdbId_AndIsNotASearch()
    {
        Link(FightClub(withSiteIds: false), MovieSite.Letterboxd).Should().Be(new MovieLink(MovieSite.Letterboxd,
            "https://letterboxd.com/imdb/tt0137523/", null, IsSearch: false));
    }

    [Fact]
    public void UnknownSiteIds_FallBackToSearch()
    {
        var movie = FightClub(withSiteIds: false);

        Link(movie, MovieSite.RottenTomatoes).Url.Should().Be("https://www.rottentomatoes.com/search?search=Fight%20Club");
        Link(movie, MovieSite.Metacritic).Url.Should().Be("https://www.metacritic.com/search/Fight%20Club/");
        Link(movie, MovieSite.Mubi).Url.Should().Be("https://mubi.com/en/search/films?query=Fight%20Club");
        MovieLinks.For(movie).Where(link => link.IsSearch).Select(link => link.Site)
            .Should().Equal(MovieSite.RottenTomatoes, MovieSite.Metacritic, MovieSite.Mubi);
    }

    [Theory]
    [InlineData("Fast & Furious", "Fast%20%26%20Furious")]
    [InlineData("#Alive", "%23Alive")]
    [InlineData("Face/Off", "Face%2FOff")]
    [InlineData("What If?", "What%20If%3F")]
    [InlineData("Amélie", "Am%C3%A9lie")]
    public void SearchLinks_EncodeTheCompleteTitle(string title, string encoded)
    {
        var movie = FightClub(withSiteIds: false);
        movie.Title = title;

        Link(movie, MovieSite.RottenTomatoes).Url.Should().EndWith($"?search={encoded}");
        Link(movie, MovieSite.Metacritic).Url.Should().EndWith($"/search/{encoded}/");
        Link(movie, MovieSite.Mubi).Url.Should().EndWith($"?query={encoded}");
        foreach (var link in MovieLinks.For(movie).Where(link => link.IsSearch))
            Uri.UnescapeDataString(new Uri(link.Url).AbsoluteUri).Should().Contain(title);
    }

    [Theory]
    [InlineData("m/x?evil=1", "https://www.rottentomatoes.com/m/x%3Fevil%3D1")]
    [InlineData("m/x#top", "https://www.rottentomatoes.com/m/x%23top")]
    [InlineData("../admin", "https://www.rottentomatoes.com/%2E%2E/admin")]
    [InlineData("m/a b", "https://www.rottentomatoes.com/m/a%20b")]
    public void SiteIds_AreEscaped_SoTheyStayOnTheMoviePage(string rottenTomatoesId, string expected)
    {
        var movie = FightClub();
        movie.RottenTomatoesId = rottenTomatoesId;

        Link(movie, MovieSite.RottenTomatoes).Url.Should().Be(expected);
        new Uri(expected).Host.Should().Be("www.rottentomatoes.com");
    }

    [Fact]
    public void AllRatings()
    {
        var links = MovieLinks.For(FightClub());

        links.Select(link => link.Rating).Should().Equal("8.8/10", null, "79%", "67/100", null);
    }

    [Theory]
    [InlineData("7", "7.0/10")]
    [InlineData("10", "10.0/10")]
    [InlineData("0", "0.0/10")]
    public void ImdbRating_HasOneDecimalPlace(string rating, string expected)
    {
        var movie = FightClub();
        movie.ImdbRating = decimal.Parse(rating, CultureInfo.InvariantCulture);

        Link(movie, MovieSite.Imdb).Rating.Should().Be(expected);
    }

    [Fact]
    public void ImdbRating_IgnoresTheMachinesCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // Uses a decimal comma
            Link(FightClub(), MovieSite.Imdb).Rating.Should().Be("8.8/10");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MissingRatings_AreNull_AndLinksAreStillReturned()
    {
        var movie = FightClub();
        movie.ImdbRating = null;
        movie.RottenTomatoes = null;
        movie.Metascore = null;

        var links = MovieLinks.For(movie);

        links.Should().HaveCount(5);
        links.Should().OnlyContain(link => link.Rating == null);
        Link(movie, MovieSite.Metacritic).Url.Should().Be("https://www.metacritic.com/movie/fight-club");
    }

    [Fact]
    public void RottenTomatoesScore_IsHidden_WithASearchLink()
    {
        var movie = FightClub();
        movie.RottenTomatoesId = null; // The score is stored, but the movie's page is unknown

        Link(movie, MovieSite.RottenTomatoes).Should().Be(new MovieLink(MovieSite.RottenTomatoes,
            "https://www.rottentomatoes.com/search?search=Fight%20Club", null, IsSearch: true));
    }

    [Fact]
    public void Metascore_IsShown_WithASearchLink()
    {
        // Only Rotten Tomatoes requires a link to the movie's own page.
        var movie = FightClub();
        movie.MetacriticId = null;

        Link(movie, MovieSite.Metacritic).Should().Be(new MovieLink(MovieSite.Metacritic,
            "https://www.metacritic.com/search/Fight%20Club/", "67/100", IsSearch: true));
    }

    [Fact]
    public void SerializesSiteAsAString()
    {
        var json = JsonSerializer.Serialize(Link(FightClub(), MovieSite.RottenTomatoes), JsonSerializerOptions.Web);

        json.Should().Be("""{"site":"rottenTomatoes","url":"https://www.rottentomatoes.com/m/fight_club","rating":"79%","isSearch":false}""");
    }

    private static MovieLink Link(Movie movie, MovieSite site) => MovieLinks.For(movie).Single(link => link.Site == site);

    private static Movie FightClub(bool withSiteIds = true) => new()
    {
        ImdbId = "tt0137523", Title = "Fight Club", Year = 1999, FetchedAt = DateTimeOffset.UtcNow,
        ImdbRating = 8.8m, RottenTomatoes = 79, Metascore = 67,
        RottenTomatoesId = withSiteIds ? "m/fight_club" : null,
        MetacriticId = withSiteIds ? "movie/fight-club" : null,
        LetterboxdId = withSiteIds ? "fight-club" : null,
        MubiId = withSiteIds ? "918" : null,
    };
}
