using Microsoft.Extensions.Options;

namespace server.Features.Movies;

public class CityMovieFinderOptions
{
    /// <summary>The time allowed for looking up all the movies of a City.</summary>
    public TimeSpan Budget { get; set; } = TimeSpan.FromSeconds(30);
}

public record FoundMovie(MovieInfo Movie, string Blurb);

/// <param name="Partial">True if a lookup failed, so some movies may be missing.</param>
public record FoundMovies(IReadOnlyList<FoundMovie> Movies, bool Partial);

/// <summary>Looks up suggested movies, and keeps the ones that really exist.</summary>
public class CityMovieFinder(
    IMovieInfoClient client,
    IOptions<CityMovieFinderOptions> options,
    ILogger<CityMovieFinder> logger)
{
    private const int MaxSuggestions = 15;
    private const int MaxMovies = 10;
    private const int BatchSize = 5;

    private record Lookup(MovieInfo? Movie, bool Failed);

    public async Task<FoundMovies> FindAsync(IEnumerable<MovieSuggestion> suggestions,
        CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(options.Value.Budget);

        // Set when OMDb refuses a request. Running lookups may still finish, but no more are started.
        var refused = false;

        List<FoundMovie> movies = [];
        var partial = false;

        // Small batches, to look up no more movies than needed.
        foreach (var batch in suggestions.Take(MaxSuggestions).Chunk(BatchSize))
        {
            // The time is up or OMDb refuses, and there are still movies to look up.
            if (refused || budget.IsCancellationRequested)
            {
                partial = true;
                break;
            }

            var lookups = await Task.WhenAll(batch.Select(LookUpAsync));

            foreach (var (suggestion, lookup) in batch.Zip(lookups))
            {
                partial |= lookup.Failed;
                if (lookup.Movie is null) continue;
                if (movies.Any(found => found.Movie.ImdbId == lookup.Movie.ImdbId)) continue;

                movies.Add(new FoundMovie(lookup.Movie, suggestion.Blurb));

                // Failures after this can't change the result.
                if (movies.Count == MaxMovies) return new FoundMovies(movies, partial);
            }
        }

        return new FoundMovies(movies, partial);

        async Task<Lookup> LookUpAsync(MovieSuggestion suggestion)
        {
            if (refused) return new Lookup(null, Failed: true);

            try
            {
                var movie = await client.FindMovieAsync(suggestion.Title, suggestion.Year, budget.Token);
                if (movie is null)
                    logger.LogWarning("No match found for suggested movie {Title} ({Year})", suggestion.Title,
                        suggestion.Year);
                return new Lookup(movie, Failed: false);
            }
            catch (MovieInfoUnavailableException ex)
            {
                logger.LogWarning(ex, "Lookup failed for suggested movie {Title}", suggestion.Title);
                if (ex.Refused) refused = true;
                return new Lookup(null, Failed: true);
            }
            // The time is up, and the caller hasn't cancelled.
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new Lookup(null, Failed: true);
            }
        }
    }
}
