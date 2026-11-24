namespace server.Features.Cities.Endpoints;

public class GetFeaturedCities : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/cities/featured", GetFeaturedCitiesHandler.Handle)
            .WithSummary("Get the curated list of UK and European cities");
    }
}
