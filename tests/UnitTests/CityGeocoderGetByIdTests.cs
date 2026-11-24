using System.Net;
using FluentAssertions;
using server.Common.Api;
using UnitTests;

namespace server.Features.Cities.Tests;

public class CityGeocoderGetByIdTests
{
    [Fact]
    public async Task KnownCity_IsReturnedInEnglish()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(Place("PPLC")));

        var city = await Geocoder(handler).GetByIdAsync(2643743);

        city.Should().Be(new CityInfo(2643743, "London", "England", "United Kingdom", "GB"));
        handler.Requests.Should().ContainSingle()
            .Which.PathAndQuery.Should().Be("/v1/get?id=2643743&language=en&format=json");
    }

    [Theory]
    [InlineData("ADM1")]
    [InlineData("PPLX")]
    public async Task PlaceThatIsNotACity_IsNull(string featureCode)
    {
        (await Get(_ => FakeHttpHandler.Json(Place(featureCode)))).Should().BeNull();
    }

    [Fact]
    public async Task UnknownId_IsNull()
    {
        (await Get(_ => FakeHttpHandler.Json("""{"reason":"Location ID not found.","error":true}""",
            HttpStatusCode.BadRequest))).Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"reason":"Something else","error":true}""")]
    [InlineData(HttpStatusCode.BadRequest, "not json")]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "null")]
    public async Task GeocoderFailure_Throws(HttpStatusCode status, string body)
    {
        var act = () => Get(_ => FakeHttpHandler.Json(body, status));

        await act.Should().ThrowAsync<GeocoderUnavailableException>();
    }

    [Theory]
    [InlineData("\"id\":1,")]
    [InlineData("")]
    public async Task DifferentOrMissingId_Throws(string id)
    {
        var body = Place("PPLC").Replace("\"id\":2643743,", id);

        var act = () => Get(_ => FakeHttpHandler.Json(body));

        await act.Should().ThrowAsync<GeocoderUnavailableException>();
    }

    [Fact]
    public async Task ConnectionError_Throws()
    {
        var act = () => Get(_ => throw new HttpRequestException("No connection"));

        await act.Should().ThrowAsync<GeocoderUnavailableException>();
    }

    private static string Place(string featureCode) =>
        $$"""
          {"id":2643743,"name":"London","admin1":"England","country":"United Kingdom","country_code":"GB","feature_code":"{{featureCode}}"}
          """;

    private static Task<CityInfo?> Get(Func<Uri, HttpResponseMessage> respond) =>
        Geocoder(new FakeHttpHandler(respond)).GetByIdAsync(2643743);

    private static CityGeocoderOpenMeteo Geocoder(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://geocoder.test/") });
}
