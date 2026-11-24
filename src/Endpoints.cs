using server.Features.Cities.Endpoints;
using server.Features.Movies.Endpoints;

namespace server;

public static class Endpoints
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        GetFeaturedCities.Map(app);
        SearchCities.Map(app);
        SearchCityMovies.Map(app);
    }
}
