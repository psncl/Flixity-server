namespace server.Features.Movies;

public class WikidataOptions
{
    /// <summary>
    /// Wikidata blocks requests without a descriptive User-Agent. Its policy asks for the app's name
    /// and a way to contact its owner: https://foundation.wikimedia.org/wiki/Policy:User-Agent_policy
    /// </summary>
    public string UserAgent { get; set; } = "Flixity/0.1 (movie filming locations app)";

    /// <summary>
    /// Limit for each attempt, including reading the response. A failure only costs links,
    /// so this is short to keep a search from waiting long.
    /// </summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long to wait before the one retry after a timeout, lost connection or 5xx.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>A 429 is retried only if its Retry-After is at most this long.</summary>
    public TimeSpan MaxRetryAfter { get; set; } = TimeSpan.FromSeconds(5);
}
