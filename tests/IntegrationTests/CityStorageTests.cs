using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Common.Database;

namespace IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class CityStorageTests(DatabaseFixture database)
{
    [Fact]
    public async Task Migrations_AreAllAppliedToPostgres()
    {
        await using var db = database.CreateDbContext();

        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateGeoNamesId_IsRejected()
    {
        await using var db = database.CreateDbContext();
        // Rolled back on dispose, so tests don't affect each other.
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Cities.AddRange(Springfield(1, "Illinois"), Springfield(1, "Missouri"));

        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SameNameAndCountry_WithDifferentGeoNamesIds_AreBothStored()
    {
        await using var db = database.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Cities.AddRange(Springfield(1, "Illinois"), Springfield(2, "Missouri"));

        await db.SaveChangesAsync();

        (await db.Cities.CountAsync(city => city.Name == "Springfield")).Should().Be(2);
    }

    private static City Springfield(int geoNamesId, string region) => new()
    {
        GeoNamesId = geoNamesId, Name = "Springfield", Region = region,
        Country = "United States", CountryCode = "US"
    };
}
