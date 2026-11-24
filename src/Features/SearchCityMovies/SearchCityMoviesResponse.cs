namespace server.Features.Movies;

/// <param name="Partial">True if some movies could not be looked up, so searching again may find more.</param>
public record SearchCityMoviesResponse(
    SearchCityMoviesResponse.CityDetails City,
    bool Partial,
    IReadOnlyList<SearchCityMoviesResponse.MovieDetails> Movies)
{
    public record CityDetails(int GeoNamesId, string Name, string? Region, string Country, string CountryCode);

    public record MovieDetails(
        string ImdbId,
        string Title,
        int Year,
        string? Director,
        string? PosterUrl,
        string? Plot,
        string Blurb);
}
