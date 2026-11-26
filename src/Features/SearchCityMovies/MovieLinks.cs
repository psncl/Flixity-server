using System.Globalization;
using System.Text.Json.Serialization;
using server.Common.Database;

namespace server.Features.Movies;

[JsonConverter(typeof(JsonStringEnumConverter<MovieSite>))]
public enum MovieSite
{
    [JsonStringEnumMemberName("imdb")] Imdb,
    [JsonStringEnumMemberName("letterboxd")] Letterboxd,
    [JsonStringEnumMemberName("rottenTomatoes")] RottenTomatoes,
    [JsonStringEnumMemberName("metacritic")] Metacritic,
    [JsonStringEnumMemberName("mubi")] Mubi,
}

/// <param name="Rating">For display, such as "8.8/10". Null when unavailable.</param>
/// <param name="IsSearch">True if the link searches the site for the title, because the Movie's own page is unknown.</param>
public record MovieLink(MovieSite Site, string Url, string? Rating, bool IsSearch);

/// <summary>
/// Builds a Movie's links from its stored site IDs and ratings. All site URL formats are here,
/// so a site changing its format needs a change only in this class.
/// </summary>
public static class MovieLinks
{
    /// <returns>Always five links, in the order IMDb, Letterboxd, Rotten Tomatoes, Metacritic, MUBI.</returns>
    public static IReadOnlyList<MovieLink> For(Movie movie)
    {
        var title = Uri.EscapeDataString(movie.Title);

        var rottenTomatoes = movie.RottenTomatoesId is { } rtId
            // Rotten Tomatoes requires scores to link to the movie's own page, so a search link has no score.
            ? new MovieLink(MovieSite.RottenTomatoes, $"https://www.rottentomatoes.com/{Path(rtId)}",
                Percent(movie.RottenTomatoes), IsSearch: false)
            : new MovieLink(MovieSite.RottenTomatoes, $"https://www.rottentomatoes.com/search?search={title}",
                Rating: null, IsSearch: true);

        return
        [
            new(MovieSite.Imdb, $"https://www.imdb.com/title/{Path(movie.ImdbId)}/", OutOf10(movie.ImdbRating), false),

            // Letterboxd redirects /imdb/{id}/ to the film, so it is never a search.
            movie.LetterboxdId is { } letterboxdId
                ? new(MovieSite.Letterboxd, $"https://letterboxd.com/film/{Path(letterboxdId)}/", null, false)
                : new(MovieSite.Letterboxd, $"https://letterboxd.com/imdb/{Path(movie.ImdbId)}/", null, false),

            rottenTomatoes,

            movie.MetacriticId is { } metacriticId
                ? new(MovieSite.Metacritic, $"https://www.metacritic.com/{Path(metacriticId)}",
                    OutOf100(movie.Metascore), false)
                : new(MovieSite.Metacritic, $"https://www.metacritic.com/search/{title}/",
                    OutOf100(movie.Metascore), true),

            movie.MubiId is { } mubiId
                ? new(MovieSite.Mubi, $"https://mubi.com/films/{Path(mubiId)}", null, false)
                : new(MovieSite.Mubi, $"https://mubi.com/en/search/films?query={title}", null, true),
        ];
    }

    /// <summary>
    /// Escapes each segment of a site ID, keeping its slashes ("m/fight_club" stays as it is).
    /// IDs come from Wikidata, which anyone can edit, so "?", "#" or ".." must not change where a link goes.
    /// </summary>
    private static string Path(string id) =>
        string.Join('/', id.Split('/').Select(segment => segment is "." or ".."
            ? Uri.EscapeDataString(segment).Replace(".", "%2E")
            : Uri.EscapeDataString(segment)));

    // InvariantCulture so that 8.8 isn't shown as "8,8" on a machine whose culture uses a decimal comma.
    private static string? OutOf10(decimal? rating) =>
        rating is { } value ? $"{value.ToString("0.0", CultureInfo.InvariantCulture)}/10" : null;

    private static string? Percent(int? score) => score is { } value ? $"{value}%" : null;

    private static string? OutOf100(int? score) => score is { } value ? $"{value}/100" : null;
}
