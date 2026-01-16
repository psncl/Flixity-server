namespace server.Features.Movies.Endpoints;

public class GetMoviesByCity : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/movies/search", GetMoviesByCityHandler.Handle)
            .WithSummary("Get a list of movies by location");
    }
}