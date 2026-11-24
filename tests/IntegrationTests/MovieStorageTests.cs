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
