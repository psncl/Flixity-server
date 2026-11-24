using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute.ExceptionExtensions;
using server.Common.Api;
using server.Common.Database;

namespace IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class GetFeaturedCitiesTests(DatabaseFixture database)
{
    // Written out instead of read from FeaturedCities, so a mistake in the seed data is noticed.
    private static readonly string[] ExpectedOrder =
    [
        "Salzburg", "Vienna", "Bruges", "Dubrovnik", "Prague", "Copenhagen", "Nice", "Paris", "Berlin", "Munich",
        "Athens", "Budapest", "Dublin", "Florence", "Rome", "Venice", "Amsterdam", "Lisbon", "Barcelona", "Madrid",
        "Seville", "Stockholm", "Bath", "Belfast", "Edinburgh", "Glasgow", "Liverpool", "London", "Manchester",
        "Oxford"
    ];

    [Fact]
    public async Task Migrations_SeedFeaturedCities()
    {
        await using var db = database.CreateDbContext();

        var cities = await db.Cities.Where(city => city.IsFeatured).ToListAsync();

        cities.Should().BeEquivalentTo(FeaturedCities.All);
        cities.Should().HaveCount(30).And.OnlyContain(city => city.GeneratedAt == null);
    }

    [Fact]
    public async Task ReturnsOnlyFeaturedCities_OrderedByCountryThenName()
    {
        await using var db = database.CreateDbContext();
        var other = new City { GeoNamesId = 4717560, Name = "Paris", Country = "United States", CountryCode = "US" };
        // Committed, unlike in CityStorageTests, because the API uses its own connection.
        db.Cities.Add(other);
        await db.SaveChangesAsync();

        try
        {
            other.Id.Should().BePositive("seeded keys are negative, so generated keys must not be");
            FeaturedCities.All.Should().OnlyContain(city => city.Id < 0);

            await using var api = new ApiFactory(database);

            var cities = await api.CreateClient().GetFromJsonAsync<CityInfo[]>("/cities/featured");

            cities!.Select(city => city.Name).Should().Equal(ExpectedOrder);
            cities.Should().Contain(new CityInfo(2643743, "London", "England", "United Kingdom", "GB"));
        }
        finally
        {
            db.Cities.Remove(other);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GeocoderUnavailable_StillReturnsCities()
    {
        await using var api = new ApiFactory(database);
        api.Geocoder.SearchAsync(default!, default!, default)
            .ThrowsAsyncForAnyArgs(new GeocoderUnavailableException("down"));

        var cities = await api.CreateClient().GetFromJsonAsync<CityInfo[]>("/cities/featured");

        cities.Should().HaveCount(30);
    }
}
