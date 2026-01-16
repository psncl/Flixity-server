namespace server.Common.Api;

public record MovieInfo(string imdbId, string title, int year, string director, string plot);

public interface IMovieInfoClient
{
    /// <summary>
    /// Search a movie API (like OMDb) for a movie by title and year.
    /// </summary>
    /// <param name="title">Movie title in English</param>
    /// <param name="year">Year of release</param>
    /// <returns>A MovieInfo object with the movie data, or null if the movie was not found or the API call failed.</returns>
    public Task<MovieInfo?> GetMovieByTitleYearAsync(string title, int year);
}