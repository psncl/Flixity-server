using server.Common.Database;

namespace server.Features.Movies;

/// <param name="Partial">True if some movies could not be looked up, so searching again may find more.</param>
public record SearchCityMoviesResponse(
    SearchCityMoviesResponse.CityDetails City,
    bool Partial,
    IReadOnlyList<SearchCityMoviesResponse.MovieDetails> Movies)
{
    public record CityDetails(int GeoNamesId, string Name, string? Region, string Country, string CountryCode);

    /// <param name="Links">Always five, in the order IMDb, Letterboxd, Rotten Tomatoes, Metacritic, MUBI.</param>
    public record MovieDetails(
        string ImdbId,
        string Title,
        int Year,
        string? Director,
        string? PosterUrl,
        string? Plot,
        string Blurb,
        IReadOnlyList<MovieLink> Links)
    {
        /// <summary>The one place a Movie becomes a response, so first and saved results can't differ.</summary>
        public static MovieDetails From(Movie movie, string blurb) => new(movie.ImdbId, movie.Title, movie.Year,
            movie.Director, movie.PosterUrl, movie.Plot, blurb, MovieLinks.For(movie));
    }
}
