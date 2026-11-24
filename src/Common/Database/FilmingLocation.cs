namespace server.Common.Database;

/// <summary>The fact that a Movie was filmed in a City.</summary>
public class FilmingLocation
{
    public int CityId { get; set; }
    public City City { get; set; } = null!;

    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public required string Blurb { get; set; }

    /// <summary>Keeps the order in which the Movies were suggested, so repeat searches look the same.</summary>
    public required int Position { get; set; }
}
