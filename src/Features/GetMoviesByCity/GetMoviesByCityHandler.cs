using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace server.Features.Movies;

public class GetMoviesByCityHandler
{
    public record Request(string Location);

    public static async Task<Results<Ok<List<MovieInfo>>, NotFound>> Handle(
        [FromBody] Request request, IConfiguration config, IMovieInfoClient movieInfoClient)
    {
        IMovieInfoLlmClient llmClient = new MovieInfoGemini(config);
        var llmResult = await llmClient.GetMovieListGeminiAsync(request.Location);

        if (llmResult is null) return TypedResults.NotFound();

        List<MovieInfo> moviesInfo = [];

        foreach (MovieInfoLlmModel movieInfo in llmResult)
        {
            var fullMetadata = await GetFullMetadata(movieInfo, movieInfoClient);
            if (fullMetadata is not null) moviesInfo.Add(fullMetadata);
        }

        return TypedResults.Ok(moviesInfo);
    }

    private static async Task<MovieInfo?> GetFullMetadata(MovieInfoLlmModel movie, IMovieInfoClient movieInfoClient)
    {
        return await movieInfoClient.GetMovieByTitleYearAsync(movie.Title, movie.Year);
    }
}