using server.Features.Movies.Endpoints;

namespace server;

public static class Endpoints
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        GetMovies.Map(app);
        GetMoviesByCity.Map(app);
    }
}