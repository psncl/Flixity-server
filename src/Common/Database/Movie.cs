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

    public required DateTimeOffset FetchedAt { get; set; }
}
