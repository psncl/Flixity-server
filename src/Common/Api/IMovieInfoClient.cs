namespace server.Common.Api;

public record MovieInfo(string ImdbId, string Title, int Year, string? Director, string? Plot, string? PosterUrl);

/// <summary>A movie lookup failed. Different from a lookup that found nothing.</summary>
/// <param name="refused">True if further lookups would fail too, e.g. because the quota is used up.</param>
public class MovieInfoUnavailableException(string message, bool refused = false, Exception? inner = null)
    : Exception(message, inner)
{
    public bool Refused { get; } = refused;
}

public interface IMovieInfoClient
{
    /// <summary>
    /// Find a movie in a movie API (like OMDb), and check that it really is the one asked for.
    /// </summary>
    /// <param name="title">Movie title in English</param>
    /// <param name="year">Year of release. The API's year can differ by one, because sources disagree. The result has this year.</param>
    /// <param name="cancellationToken">Cancels the lookup</param>
    /// <returns>The movie, or null if no matching movie was found.</returns>
    /// <exception cref="MovieInfoUnavailableException">The API failed, timed out or refused the request.</exception>
    public Task<MovieInfo?> FindMovieAsync(string title, int year, CancellationToken cancellationToken = default);
}
