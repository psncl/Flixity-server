namespace server.Features.Movies.Endpoints;

public class GetMovies : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/movies", GetMoviesHandler.Handle)
            .WithSummary("Search a movie by title and year");
    }
}