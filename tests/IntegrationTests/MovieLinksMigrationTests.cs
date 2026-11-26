using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using server.Common.Database;

namespace IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class MovieLinksMigrationTests(DatabaseFixture database)
{
    [Fact]
    public async Task AddMovieLinksAndRatings_DeletesSavedResults_AndKeepsCities()
    {
        // A separate database in the shared container, so it can start at the previous migration.
        // EF Core creates it on the first migration.
        var connectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Database = "migration_test",
        }.ConnectionString;
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("AddMovies");
        // Raw SQL, because the current Movie model has columns this schema doesn't have yet.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Cities" ("GeoNamesId", "Name", "Country", "CountryCode", "IsFeatured", "GeneratedAt")
            VALUES (999001, 'Testville', 'Testland', 'TL', false, now());
            INSERT INTO "Movies" ("ImdbId", "Title", "Year", "FetchedAt")
            VALUES ('tt0137523', 'Fight Club', 1999, now());
            INSERT INTO "FilmingLocations" ("CityId", "MovieId", "Blurb", "Position")
            SELECT c."Id", m."Id", 'A blurb.', 0 FROM "Cities" c, "Movies" m WHERE c."GeoNamesId" = 999001;
            UPDATE "Cities" SET "GeneratedAt" = now() WHERE "IsFeatured";
            """);
        var citiesBefore = await db.Database.SqlQueryRaw<int>("""SELECT count(*) AS "Value" FROM "Cities" """).SingleAsync();

        await migrator.MigrateAsync();

        (await db.FilmingLocations.CountAsync()).Should().Be(0);
        (await db.Movies.CountAsync()).Should().Be(0);
        (await db.Cities.CountAsync()).Should().Be(citiesBefore);
        (await db.Cities.CountAsync(city => city.IsFeatured)).Should().Be(FeaturedCities.All.Length);
        (await db.Cities.AnyAsync(city => city.GeneratedAt != null)).Should().BeFalse();

        await db.Database.EnsureDeletedAsync();
    }
}
