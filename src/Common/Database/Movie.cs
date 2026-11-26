namespace server.Common.Database;

public class Movie
{
    public int Id { get; set; }
    public required string ImdbId { get; set; }
    public required string Title { get; set; }
    public required int Year { get; set; }

    // OMDb doesn't have these for every movie.
    public string? Director { get; set; }
    public string? Plot { get; set; }
    public string? PosterUrl { get; set; }

    // Ratings from OMDb, saved when the Movie is first stored and never refreshed.
    /// <summary>IMDb rating out of 10, such as 8.8.</summary>
    public decimal? ImdbRating { get; set; }
    /// <summary>Rotten Tomatoes Tomatometer, 0 to 100.</summary>
    public int? RottenTomatoes { get; set; }
    /// <summary>Metacritic Metascore, 0 to 100.</summary>
    public int? Metascore { get; set; }

    // Site IDs from Wikidata. Only IDs are stored, so link URLs are built in code
    // and a site's URL format can change without a data update.
    /// <summary>Such as "m/fight_club".</summary>
    public string? RottenTomatoesId { get; set; }
    /// <summary>Such as "movie/fight-club".</summary>
    public string? MetacriticId { get; set; }
    /// <summary>Such as "fight-club".</summary>
    public string? LetterboxdId { get; set; }
    /// <summary>Such as "918".</summary>
    public string? MubiId { get; set; }

    public required DateTimeOffset FetchedAt { get; set; }
}
