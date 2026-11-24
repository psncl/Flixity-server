using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using server.Common.Api;

namespace IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class SearchCitiesTests : IDisposable
{
    private static readonly CityInfo[] Parises =
    [
        new(2988507, "Paris", "Île-de-France Region", "France", "FR"),
        new(4717560, "Paris", "Texas", "United States", "US")
    ];

    private readonly DatabaseFixture _database;
    private readonly ApiFactory _api;

    public SearchCitiesTests(DatabaseFixture database)
    {
        _database = database;
        _api = new ApiFactory(database);
        GeocoderReturns(Parises);
    }

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task AmbiguousName_ReturnsEachCity()
    {
        var cities = await _api.CreateClient().GetFromJsonAsync<CityInfo[]>("/cities?query=paris");

        cities.Should().Equal(Parises);
    }

    [Fact]
    public async Task Response_UsesDocumentedPropertyNames()
    {
        GeocoderReturns(new CityInfo(1, "Monaco", null, "Monaco", "MC"));

        var json = await _api.CreateClient().GetStringAsync("/cities?query=monaco");

        json.Should().Be(
            """[{"geoNamesId":1,"name":"Monaco","region":null,"country":"Monaco","countryCode":"MC"}]""");
    }

    [Fact]
    public async Task NoMatch_ReturnsEmptyList()
    {
        GeocoderReturns();

        var response = await _api.CreateClient().GetAsync("/cities?query=asdfgh");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    public static TheoryData<string, string> InvalidRequests()
    {
        var data = new TheoryData<string, string>();
        foreach (var environment in new[] { "Development", "Production" })
        {
            data.Add(environment, "/cities");
            data.Add(environment, "/cities?query=");
            data.Add(environment, "/cities?query=p");
            data.Add(environment, "/cities?query=%20p%20");
            data.Add(environment, "/cities?query=%20%20%20");
            data.Add(environment, "/cities?query=" + new string('p', 101));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidQuery_Returns400WithoutContactingGeocoder(string environment, string url)
    {
        await using var api = new ApiFactory(_database, environment);

        var response = await api.CreateClient().GetAsync(url);

        await ShouldBeProblem(response, HttpStatusCode.BadRequest);
        api.Geocoder.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidQuery_ReturnsProblemDetails_EvenIfClientPrefersHtml(string environment, string url)
    {
        await using var api = new ApiFactory(_database, environment);
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "text/html");

        await ShouldBeProblem(await client.GetAsync(url), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public async Task LengthLimits_AreInclusive_AndIgnoreSurroundingSpaces(int length)
    {
        var name = new string('p', length);

        var response = await _api.CreateClient().GetAsync($"/cities?query=%20{name}%20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _api.Geocoder.Received(1).SearchAsync(name, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("*", "en")]
    [InlineData("fr", "fr")]
    [InlineData("fr-CA,fr;q=0.9,en;q=0.8", "fr")]
    [InlineData("en;q=0.5,FR;q=0.9", "fr")]
    [InlineData("de;q=0", "en")]
    public async Task PreferredLanguage_IsPassedToGeocoder(string? header, string expected)
    {
        var client = _api.CreateClient();
        if (header is not null) client.DefaultRequestHeaders.Add("Accept-Language", header);

        await client.GetAsync("/cities?query=Londres");

        await _api.Geocoder.Received(1).SearchAsync("Londres", expected, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task GeocoderFailure_Returns503WithoutUpstreamDetails(string environment)
    {
        await using var api = new ApiFactory(_database, environment);
        api.Geocoder.SearchAsync(default!, default!, default).ThrowsAsyncForAnyArgs(
            new GeocoderUnavailableException("secret-outer", new HttpRequestException("secret-inner")));

        var response = await api.CreateClient().GetAsync("/cities?query=paris");

        await ShouldBeProblem(response, HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("secret");
    }

    [Fact]
    public async Task Suggestions_DoNotChangeStoredCities()
    {
        var before = await StoredCities();

        await _api.CreateClient().GetAsync("/cities?query=paris");

        (await StoredCities()).Should().Equal(before);
    }

    private async Task<List<int>> StoredCities()
    {
        await using var db = _database.CreateDbContext();
        return await db.Cities.Select(city => city.GeoNamesId).Order().ToListAsync();
    }

    private void GeocoderReturns(params CityInfo[] cities) =>
        _api.Geocoder.SearchAsync(default!, default!, default).ReturnsForAnyArgs(cities);

    private static async Task ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.Should().Be((int)status);
    }
}
