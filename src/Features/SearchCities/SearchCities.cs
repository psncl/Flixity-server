namespace server.Features.Cities.Endpoints;

public class SearchCities : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/cities", SearchCitiesHandler.Handle)
            .WithSummary("Suggest cities matching typed text")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
