using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Common.Database;

namespace IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class MovieStorageTests(DatabaseFixture database)
{
    [Fact]
    public async Task DuplicateImdbId_IsRejected()
    {
        await using var db = database.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Movies.AddRange(Movie("tt1"), Movie("tt1"));

        await ShouldViolateUniqueness(db);
    }

    [Fact]
    public async Task MovieHasOneFilmingLocationPerCity()
    {
        await using var db = database.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (city, movie) = (City(1), Movie("tt1"));
        db.FilmingLocations.Add(Location(city, movie));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); // Otherwise EF Core notices before Postgres does

        db.FilmingLocations.Add(new FilmingLocation { CityId = city.Id, MovieId = movie.Id, Blurb = "b", Position = 1 });

        await ShouldViolateUniqueness(db);
    }

    [Fact]
    public async Task DeletingCity_DeletesItsFilmingLocations_ButKeepsSharedMovies()
    {
        await using var db = database.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (first, second, movie) = (City(1), City(2), Movie("tt1"));
        db.FilmingLocations.AddRange(Location(first, movie), Location(second, movie));
        await db.SaveChangesAsync();

        // Runs directly in the database, so this checks Postgres's cascade and not EF Core's.
        await db.Cities.Where(city => city.Id == first.Id).ExecuteDeleteAsync();

        (await db.FilmingLocations.Where(l => l.MovieId == movie.Id).Select(l => l.CityId).ToListAsync())
            .Should().Equal(second.Id);
        (await db.Movies.AnyAsync(m => m.Id == movie.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletingMovie_DeletesItsFilmingLocations_ButKeepsCities()
    {
        await using var db = database.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (city, movie) = (City(1), Movie("tt1"));
        db.FilmingLocations.Add(Location(city, movie));
        await db.SaveChangesAsync();

        await db.Movies.Where(m => m.Id == movie.Id).ExecuteDeleteAsync();

        (await db.FilmingLocations.AnyAsync(l => l.CityId == city.Id)).Should().BeFalse();
        (await db.Cities.AnyAsync(c => c.Id == city.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task MovieWithRatingsAndSiteIds_IsReloadedUnchanged()
    {
        await using var db = database.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var movie = Movie("tt0137523");
        movie.ImdbRating = 8.8m;
        movie.RottenTomatoes = 79;
        movie.Metascore = 67;
        movie.RottenTomatoesId = "m/fight_club";
        movie.MetacriticId = "movie/fight-club";
        movie.LetterboxdId = "fight-club";
        movie.MubiId = "918";
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); // So the Movie is read from Postgres, not EF Core's tracked copy

        var reloaded = await db.Movies.SingleAsync(m => m.ImdbId == "tt0137523");

        // FetchedAt is left out: Postgres stores microseconds, .NET has finer ticks.
        reloaded.Should().BeEquivalentTo(movie, options => options.Excluding(m => m.FetchedAt));
    }

    [Fact]
    public async Task MovieWithoutRatingsOrSiteIds_IsReloadedWithNulls()
    {
        await using var db = database.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Movies.Add(Movie("tt1"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.Movies.SingleAsync(m => m.ImdbId == "tt1");

        reloaded.ImdbRating.Should().BeNull();
        reloaded.RottenTomatoes.Should().BeNull();
        reloaded.Metascore.Should().BeNull();
        reloaded.RottenTomatoesId.Should().BeNull();
        reloaded.MetacriticId.Should().BeNull();
        reloaded.LetterboxdId.Should().BeNull();
        reloaded.MubiId.Should().BeNull();
    }

    [Fact]
    public async Task ImdbRatingWithMoreThanOneDecimal_IsRoundedToOne()
    {
        await using var db = database.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var movie = Movie("tt1");
        movie.ImdbRating = 7.25m;
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // numeric(3,1) rounds half away from zero.
        (await db.Movies.SingleAsync(m => m.ImdbId == "tt1")).ImdbRating.Should().Be(7.3m);
    }

    private static async Task ShouldViolateUniqueness(AppDbContext db)
    {
        var act = () => db.SaveChangesAsync();
        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    private static City City(int geoNamesId) => new()
        { GeoNamesId = geoNamesId, Name = "City", Country = "Country", CountryCode = "CC" };

    private static Movie Movie(string imdbId) => new()
        { ImdbId = imdbId, Title = "Movie", Year = 2000, FetchedAt = DateTimeOffset.UtcNow };

    private static FilmingLocation Location(City city, Movie movie) => new()
        { City = city, Movie = movie, Blurb = "Blurb", Position = 0 };
}
