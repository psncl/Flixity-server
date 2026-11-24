using System.Net;
using FluentAssertions;
using server.Common.Api;

namespace server.Features.Cities.Tests;

public class CityGeocoderOpenMeteoTests
{
    private const string Paris = """
        {"id":2988507,"name":"Paris","admin1":"Île-de-France Region","country":"France","country_code":"FR","feature_code":"PPLC"}
        """;

    [Fact]
    public async Task Search_EncodesNameAndSendsLanguage()
    {
        var handler = new FakeHandler(_ => Json("{}"));

        await Geocoder(handler).SearchAsync("a&b=c é", "fr");

        handler.RequestUri!.AbsolutePath.Should().Be("/v1/search");
        handler.RequestUri.Query.Should().Contain("name=a%26b%3Dc%20%C3%A9&").And.Contain("language=fr");
    }

    [Fact]
    public async Task Search_MapsPlaces()
    {
        var cities = await Search($$"""{"results":[{{Paris}}]}""");

        cities.Should().Equal(new CityInfo(2988507, "Paris", "Île-de-France Region", "France", "FR"));
    }

    [Fact]
    public async Task Search_PlaceWithoutRegion_HasNullRegion()
    {
        var cities = await Search("""
            {"results":[{"id":1,"name":"Monaco","country":"Monaco","country_code":"MC","feature_code":"PPLC"}]}
            """);

        cities.Should().ContainSingle().Which.Region.Should().BeNull();
    }

    [Fact]
    public async Task Search_KeepsOnlyPopulatedPlaces()
    {
        var cities = await Search($$"""
            {"results":[
              {"id":1,"name":"Mont Paris","country":"France","country_code":"FR","feature_code":"MT"},
              {"id":2,"name":"Paris Region","country":"France","country_code":"FR","feature_code":"ADM1"},
              {"id":3,"name":"No code","country":"France","country_code":"FR"},
              {{Paris}}
            ]}
            """);

        cities.Select(city => city.GeoNamesId).Should().Equal(2988507);
    }

    [Theory]
    [InlineData("PPLQ")] // Abandoned
    [InlineData("PPLH")] // Historical
    [InlineData("PPLCH")] // Historical capital
    [InlineData("PPLW")] // Destroyed
    [InlineData("PPLX")] // Section of a city
    public async Task Search_DropsPlacesThatAreNotCurrentCities(string featureCode)
    {
        (await Search($$"""{"results":[{{Place(1, featureCode)}}]}""")).Should().BeEmpty();
    }

    [Fact]
    public async Task Search_IgnoresNullAndNamelessPlaces()
    {
        var cities = await Search($$"""
            {"results":[null,{"id":1,"country":"France","country_code":"FR","feature_code":"PPL"},{{Paris}}]}
            """);

        cities.Select(city => city.GeoNamesId).Should().Equal(2988507);
    }

    [Fact]
    public async Task Search_FiltersBeforeLimitingToTen()
    {
        var mountains = Enumerable.Range(1, 10).Select(id => Place(id, "MT"));
        var towns = Enumerable.Range(101, 12).Select(id => Place(id, "PPL"));

        var cities = await Search($$"""{"results":[{{string.Join(",", mountains.Concat(towns))}}]}""");

        cities.Select(city => city.GeoNamesId).Should().Equal(Enumerable.Range(101, 10));
    }

    [Fact]
    public async Task Search_MissingResults_IsNoMatch()
    {
        (await Search("""{"generationtime_ms":0.1}""")).Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Search_HttpError_IsFailure(HttpStatusCode status)
    {
        await ShouldFail(_ => new HttpResponseMessage(status));
    }

    [Fact]
    public async Task Search_ConnectionError_IsFailure()
    {
        await ShouldFail(_ => throw new HttpRequestException("No connection"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task Search_InvalidBody_IsFailure(string body)
    {
        await ShouldFail(_ => Json(body));
    }

    [Fact]
    public async Task Search_Timeout_IsFailure()
    {
        // HttpClient's own timeout fires, because the fake never answers.
        var geocoder = Geocoder(new NeverRespondingHandler(), TimeSpan.FromMilliseconds(50));

        var act = () => geocoder.SearchAsync("paris", "en");

        await act.Should().ThrowAsync<GeocoderUnavailableException>();
    }

    [Fact]
    public async Task Search_CancelledByCaller_IsNotReportedAsFailure()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = () => Geocoder(new NeverRespondingHandler()).SearchAsync("paris", "en", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static string Place(int id, string featureCode) =>
        $$"""{"id":{{id}},"name":"Place","country":"France","country_code":"FR","feature_code":"{{featureCode}}"}""";

    private static Task<IReadOnlyList<CityInfo>> Search(string json) =>
        Geocoder(new FakeHandler(_ => Json(json))).SearchAsync("paris", "en");

    private static async Task ShouldFail(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var act = () => Geocoder(new FakeHandler(respond)).SearchAsync("paris", "en");
        await act.Should().ThrowAsync<GeocoderUnavailableException>();
    }

    private static CityGeocoderOpenMeteo Geocoder(HttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://geocoder.test/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        });

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    private class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

    private class NeverRespondingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }
}
