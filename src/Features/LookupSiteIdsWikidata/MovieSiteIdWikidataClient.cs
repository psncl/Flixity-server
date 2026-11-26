using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace server.Features.Movies;

/// <summary>
/// Finds Movies' site IDs on Wikidata by IMDb ID. Wikidata needs no key, and its data is CC0.
/// A failure only costs links, so it returns an empty result instead of failing the search.
/// </summary>
public partial class MovieSiteIdWikidataClient(
    HttpClient client,
    IOptions<WikidataOptions> options,
    ILogger<MovieSiteIdWikidataClient> logger) : IMovieSiteIdClient
{
    private static readonly IReadOnlyDictionary<string, MovieSiteIds> None = new Dictionary<string, MovieSiteIds>();

    // SPARQL results: one binding per row, holding only the variables that have a value.
    private record SparqlResponse([property: JsonPropertyName("results")] SparqlResults? Results);

    private record SparqlResults([property: JsonPropertyName("bindings")] List<Dictionary<string, SparqlValue>?>? Bindings);

    private record SparqlValue([property: JsonPropertyName("value")] string? Value);

    /// <summary>Wikidata is over its limits. Null RetryAfter means it didn't say when to try again.</summary>
    private class RateLimitedException(TimeSpan? retryAfter) : Exception("Wikidata returned 429.")
    {
        public TimeSpan? RetryAfter { get; } = retryAfter;
    }

    public async Task<IReadOnlyDictionary<string, MovieSiteIds>> FindSiteIdsAsync(
        IReadOnlyCollection<string> imdbIds, CancellationToken cancellationToken = default)
    {
        var validIds = imdbIds.Where(id => ImdbIdFormat().IsMatch(id)).Distinct().ToList();
        if (validIds.Count == 0) return None;

        for (var attempt = 1;; attempt++)
        {
            try
            {
                return await GetWithTimeoutAsync(validIds, cancellationToken);
            }
            // If the caller cancelled, let that through: it is not a Wikidata failure.
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                var retryDelay = attempt < 2 ? RetryDelay(ex) : null;
                if (retryDelay is null)
                {
                    logger.LogWarning(ex, "Wikidata site ID lookup failed after {Attempts} attempt(s)", attempt);
                    return None;
                }

                await Task.Delay(retryDelay.Value, cancellationToken);
            }
        }
    }

    /// <returns>How long to wait before retrying, or null if retrying won't help.</returns>
    private TimeSpan? RetryDelay(Exception ex) => ex switch
    {
        RateLimitedException { RetryAfter: { } wait } when wait <= options.Value.MaxRetryAfter => wait,
        RateLimitedException => null, // No Retry-After, or longer than a search should wait
        OperationCanceledException => options.Value.RetryDelay, // Timeout, since the caller hasn't cancelled
        HttpRequestException { StatusCode: null } => options.Value.RetryDelay, // No connection
        HttpRequestException { StatusCode: { } status } when (int)status >= 500 => options.Value.RetryDelay,
        _ => null // Other 4xx, invalid JSON
    };

    private async Task<IReadOnlyDictionary<string, MovieSiteIds>> GetWithTimeoutAsync(List<string> imdbIds,
        CancellationToken cancellationToken)
    {
        // The timeout covers the whole attempt, including reading the body.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.AttemptTimeout);
        return await GetAsync(imdbIds, timeout.Token);
    }

    private async Task<IReadOnlyDictionary<string, MovieSiteIds>> GetAsync(List<string> validIds,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"sparql?query={Uri.EscapeDataString(Query(validIds))}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));
        request.Headers.UserAgent.ParseAdd(options.Value.UserAgent);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new RateLimitedException(RetryAfter(response.Headers.RetryAfter));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SparqlResponse>(cancellationToken);

        var siteIds = new Dictionary<string, MovieSiteIds>();
        foreach (var row in body?.Results?.Bindings ?? [])
        {
            if (row is null) continue;
            var imdbId = Value(row, "imdb");
            // Only accept rows for IDs that were asked for.
            if (imdbId is null || !validIds.Contains(imdbId)) continue;

            siteIds[imdbId] = new MovieSiteIds(Value(row, "rottenTomatoes"), Value(row, "metacritic"),
                Value(row, "letterboxd"), Value(row, "mubi"));
        }

        return siteIds;
    }

    // Retry-After is either a number of seconds or a date.
    private static TimeSpan? RetryAfter(RetryConditionHeaderValue? header)
    {
        if (header?.Delta is { } delta) return delta;
        if (header?.Date is not { } date) return null;

        var left = date - DateTimeOffset.UtcNow;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero; // A date in the past means now
    }

    private static string? Value(Dictionary<string, SparqlValue> row, string name) =>
        row.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value.Value) ? value.Value : null;

    /// <summary>
    /// Films by IMDb ID (P345), with each site's ID: Rotten Tomatoes (P1258), Metacritic (P1712),
    /// Letterboxd (P6127) and MUBI (P7299). Grouping gives one row per IMDb ID, and MIN picks
    /// the same value every time when a film has several.
    /// </summary>
    private static string Query(IEnumerable<string> imdbIds) => $$"""
        SELECT ?imdb (MIN(?rt) AS ?rottenTomatoes) (MIN(?mc) AS ?metacritic)
               (MIN(?lb) AS ?letterboxd) (MIN(?mu) AS ?mubi)
        WHERE {
          VALUES ?imdb { {{string.Join(" ", imdbIds.Select(id => $"\"{id}\""))}} }
          ?film wdt:P345 ?imdb .
          OPTIONAL { ?film wdt:P1258 ?rt . }
          OPTIONAL { ?film wdt:P1712 ?mc . }
          OPTIONAL { ?film wdt:P6127 ?lb . }
          OPTIONAL { ?film wdt:P7299 ?mu . }
        }
        GROUP BY ?imdb
        """;

    // Checked before an ID goes into the query, so nothing else can be inserted into it.
    [GeneratedRegex("^tt[0-9]{1,10}$")]
    private static partial Regex ImdbIdFormat();
}
