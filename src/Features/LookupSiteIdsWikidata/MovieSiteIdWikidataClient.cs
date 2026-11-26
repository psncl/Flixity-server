using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace server.Features.Movies;

/// <summary>Finds Movies' site IDs on Wikidata by IMDb ID. Wikidata needs no key, and its data is CC0.</summary>
public partial class MovieSiteIdWikidataClient(HttpClient client, IOptions<WikidataOptions> options)
    : IMovieSiteIdClient
{
    // SPARQL results: one binding per row, holding only the variables that have a value.
    private record SparqlResponse([property: JsonPropertyName("results")] SparqlResults? Results);

    private record SparqlResults([property: JsonPropertyName("bindings")] List<Dictionary<string, SparqlValue>?>? Bindings);

    private record SparqlValue([property: JsonPropertyName("value")] string? Value);

    public async Task<IReadOnlyDictionary<string, MovieSiteIds>> FindSiteIdsAsync(
        IReadOnlyCollection<string> imdbIds, CancellationToken cancellationToken = default)
    {
        var validIds = imdbIds.Where(id => ImdbIdFormat().IsMatch(id)).Distinct().ToList();
        if (validIds.Count == 0) return new Dictionary<string, MovieSiteIds>();

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"sparql?query={Uri.EscapeDataString(Query(validIds))}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));
        request.Headers.UserAgent.ParseAdd(options.Value.UserAgent);

        using var response = await client.SendAsync(request, cancellationToken);
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
