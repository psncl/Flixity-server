using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace server.Features.Movies;

public class GetMoviesByCityHandler
{
    public record Request(string Location);

    public static async Task<Results<Ok<List<MovieInfoGemini.MovieInfoModel>>, NotFound>> Handle(
        [FromBody] Request request, IConfiguration config)
    {
        IMovieInfoLlmClient geminiClient = new MovieInfoGemini(config);
        var result = await geminiClient.GetMovieListGeminiAsync(request.Location);

        return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }
}