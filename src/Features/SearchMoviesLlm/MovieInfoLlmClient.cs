using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace server.Features.Movies;

/// <summary>Works with any LLM provider, because it only depends on IChatClient.</summary>
public class MovieInfoLlmClient(
    Lazy<IChatClient> chatClient,
    IOptions<LlmOptions> options,
    ILogger<MovieInfoLlmClient> logger) : IMovieInfoLlmClient
{
    private const int Attempts = 2;
    private const int MaxBlurbLength = 300;

    // The JSON schema sent to the LLM is generated from these types.
    private record Response(List<Suggestion?>? Movies);

    private record Suggestion(
        [property: Description("Title exactly as IMDb lists it in English, including accents")]
        string? Title,
        [property: Description("Release year according to IMDb")]
        int? Year,
        [property: Description("2-3 short lines, at most 250 characters, about where in the city it was filmed")]
        string? Blurb
    );

    public async Task<IReadOnlyList<MovieSuggestion>> GetMoviesFilmedInAsync(CityInfo city,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1;; attempt++)
        {
            try
            {
                return await AskAsync(city, cancellationToken);
            }
            // Providers throw different exception types, so catch all of them unless the caller cancelled.
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "LLM attempt {Attempt} failed for City {GeoNamesId}", attempt, city.GeoNamesId);
                if (attempt == Attempts) throw new LlmUnavailableException("The LLM failed.", ex);
            }
        }
    }

    private async Task<IReadOnlyList<MovieSuggestion>> AskAsync(CityInfo city, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.Timeout);

        var response = await chatClient.Value.GetResponseAsync<Response>(Prompt(city),
            cancellationToken: timeout.Token);

        if (!response.TryGetResult(out var result) || result.Movies is null)
            throw new InvalidOperationException("The LLM returned unusable data.");

        return result.Movies
            .OfType<Suggestion>() // Drops nulls, which JSON lists can contain
            .Where(movie => !string.IsNullOrWhiteSpace(movie.Title) && movie.Year is not null &&
                            !string.IsNullOrWhiteSpace(movie.Blurb))
            .Select(movie => new MovieSuggestion(movie.Title!.Trim(), movie.Year!.Value, Shorten(movie.Blurb!.Trim())))
            .ToList();
    }

    private static string Prompt(CityInfo city)
    {
        var name = city.Region is null ? $"{city.Name}, {city.Country}" : $"{city.Name}, {city.Region}, {city.Country}";

        return $"""
                List up to 15 feature films that were filmed on location in this city: {name}.

                - Only include films with scenes really filmed in the city. Leave out films that are merely set there.
                - Only include a film if you can name a specific place in the city (a street, landmark or
                  neighbourhood) where it was filmed. Fewer than 15 films, or none, is a good answer.
                - The blurb must be about filming in this city only, and must name the specific place.
                - Put the best-known films first.
                """;
    }

    private static string Shorten(string text)
    {
        if (text.Length <= MaxBlurbLength) return text;

        // Leave room for the ellipsis. Cut at the last whitespace, unless that loses more than half.
        const int limit = MaxBlurbLength - 1;
        var end = limit;
        while (end > limit / 2 && !char.IsWhiteSpace(text[end])) end--;
        if (end <= limit / 2) end = limit;

        // Some characters, like emoji, take two chars. Don't cut between them.
        if (char.IsHighSurrogate(text[end - 1])) end--;

        return text[..end].TrimEnd().TrimEnd(',', '.', ';', ':') + "…";
    }
}
