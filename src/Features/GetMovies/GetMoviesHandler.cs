using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace server.Features.Movies;

public class GetMoviesHandler
{
    public static async Task<Results<Ok<MovieInfo>, NotFound>> Handle([FromQuery] string title, [FromQuery] int year,
        IGetMovieInfoClient omdbClient)
    {
        var movie = await omdbClient.GetMovieByTitleYearAsync(title, year);
        return movie is null ? TypedResults.NotFound() : TypedResults.Ok(movie);
    }
}