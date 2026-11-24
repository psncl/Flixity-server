using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using server.Common.Database;

namespace IntegrationTests;

/// <summary>Uses the real LLM client, without an API key.</summary>
[Collection(DatabaseCollection.Name)]
public class MissingLlmKeyTests(DatabaseFixture database) : IAsyncLifetime
{
    private const int CityId = 900_000_201;

    private readonly WebApplicationFactory<Program> _api = new ApiFactory(database)
        .WithWebHostBuilder(builder => builder.UseSetting("OPENAI_API_KEY", ""));

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
        await using var db = database.CreateDbContext();
        await db.Cities.Where(city => city.GeoNamesId == CityId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task CachedSearch_Works()
    {
        await StoreCityAsync(generatedAt: DateTimeOffset.UtcNow);

        (await SearchAsync()).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UncachedSearch_Is503WithoutDetails()
    {
        await StoreCityAsync(generatedAt: null);

        var response = await SearchAsync();

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("OPENAI");
    }

    private Task<HttpResponseMessage> SearchAsync() =>
        _api.CreateClient().PostAsync($"/cities/{CityId}/search", null);

    private async Task StoreCityAsync(DateTimeOffset? generatedAt)
    {
        await using var db = database.CreateDbContext();
        db.Cities.Add(new City
        {
            GeoNamesId = CityId, Name = "Testville", Country = "Testland", CountryCode = "TL",
            GeneratedAt = generatedAt
        });
        await db.SaveChangesAsync();
    }
}
