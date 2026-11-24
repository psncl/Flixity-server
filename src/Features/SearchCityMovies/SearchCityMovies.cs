namespace server.Features.Movies.Endpoints;

public class SearchCityMovies : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // POST and not GET, because a search can cost money and save data. Crawlers shouldn't trigger that.
        app.MapPost("/cities/{geoNamesId}/search", SearchCityMoviesHandler.Handle)
            .WithSummary("Get movies filmed in a city")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
