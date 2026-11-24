namespace server.Common.Api;

public record MovieSuggestion(string Title, int Year, string Blurb);

/// <summary>The LLM failed or kept returning unusable data.</summary>
public class LlmUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public interface IMovieInfoLlmClient
{
    /// <summary>
    /// Ask an LLM for movies filmed in a City. The answers are suggestions: the LLM can be wrong.
    /// </summary>
    /// <returns>Suggestions in the LLM's order. Can be empty.</returns>
    /// <exception cref="LlmUnavailableException">Both attempts failed.</exception>
    public Task<IReadOnlyList<MovieSuggestion>> GetMoviesFilmedInAsync(CityInfo city,
        CancellationToken cancellationToken = default);
}
