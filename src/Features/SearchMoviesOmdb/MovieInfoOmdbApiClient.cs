using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace server.Features.Movies;

public partial class MovieInfoOmdbApiClient(HttpClient client, IOptions<OmdbOptions> options) : IMovieInfoClient
{
    private record OmdbApiResponse(
        string? Title,
        string? Year,
        string? Type,
        string? Director,
        string? Plot,
        string? Poster,
        [property: JsonPropertyName("imdbID")] string? ImdbId,
        [property: JsonPropertyName("imdbRating")] string? ImdbRating,
        string? Metascore,
        List<OmdbRating>? Ratings,
        string? Response,
        string? Error
    );

    /// <summary>One entry of OMDb's Ratings list, such as Source "Rotten Tomatoes" with Value "79%".</summary>
    private record OmdbRating(string? Source, string? Value);

    public async Task<MovieInfo?> FindMovieAsync(string title, int year, CancellationToken cancellationToken = default)
    {
        // Look up without the year as well, because OMDb's year can differ from the one we have.
        return await FindAsync($"t={Uri.EscapeDataString(title)}&y={year}") ??
               await FindAsync($"t={Uri.EscapeDataString(title)}");

        async Task<MovieInfo?> FindAsync(string query)
        {
            var movie = ToMovie(await GetWithRetriesAsync(query, cancellationToken));
            if (movie is null || !MovieMatch.IsMatch(title, year, movie.Title, movie.Year)) return null;

            // OMDb's year is often one later than the original release, so keep the one asked for.
            return movie with { Year = year };
        }
    }

    private static MovieInfo? ToMovie(OmdbApiResponse? response)
    {
        if (response is null || response.Type != "movie") return null;
        if (string.IsNullOrEmpty(response.ImdbId) || string.IsNullOrEmpty(response.Title)) return null;

        // Some years are ranges, e.g. "2001–2003".
        var year = FirstYear().Match(response.Year ?? "");
        if (!year.Success) return null;

        var rottenTomatoes = response.Ratings?.FirstOrDefault(rating => rating.Source == "Rotten Tomatoes")?.Value;

        return new MovieInfo(response.ImdbId, response.Title, int.Parse(year.Value), NullIfMissing(response.Director),
            NullIfMissing(response.Plot), NullIfMissing(response.Poster),
            ImdbRating: ParseImdbRating(response.ImdbRating),
            RottenTomatoes: ParseScore(rottenTomatoes, suffix: "%"),
            Metascore: ParseScore(response.Metascore));
    }

    // Ratings are optional, so anything unreadable ("N/A", "abc", out of range) is null rather than an error.
    // InvariantCulture so that "8.8" parses the same on a machine whose culture uses a decimal comma.

    /// <summary>"8.8" to 8.8, if between 0 and 10.</summary>
    private static decimal? ParseImdbRating(string? value) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rating)
        && rating <= 10
            ? rating
            : null;

    /// <summary>"67" (or "79%" with the suffix) to 67, if between 0 and 100.</summary>
    private static int? ParseScore(string? value, string suffix = "")
    {
        if (value is null || !value.EndsWith(suffix, StringComparison.Ordinal)) return null;

        return int.TryParse(value.AsSpan(0, value.Length - suffix.Length), NumberStyles.None,
            CultureInfo.InvariantCulture, out var score) && score <= 100
            ? score
            : null;
    }

    private static string? NullIfMissing(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "N/A" ? null : value;

    [GeneratedRegex("^[0-9]{4}")]
    private static partial Regex FirstYear();

    private async Task<OmdbApiResponse?> GetWithRetriesAsync(string query, CancellationToken cancellationToken)
    {
        for (var attempt = 0;; attempt++)
        {
            try
            {
                return await GetAsync(query, cancellationToken);
            }
            // A timeout also throws OperationCanceledException, but then the caller hasn't cancelled.
            catch (Exception ex) when (IsTemporary(ex) && !cancellationToken.IsCancellationRequested)
            {
                if (attempt == options.Value.RetryDelays.Length)
                    throw new MovieInfoUnavailableException("The movie lookup kept failing.", inner: ex);

                await Task.Delay(options.Value.RetryDelays[attempt], cancellationToken);
            }
        }
    }

    private static bool IsTemporary(Exception ex) => ex switch
    {
        OperationCanceledException => true, // Timeout
        HttpRequestException { StatusCode: null } => true, // No connection
        HttpRequestException { StatusCode: { } status } => (int)status >= 500,
        _ => false
    };

    /// <returns>Null if OMDb has no such movie.</returns>
    private async Task<OmdbApiResponse?> GetAsync(string query, CancellationToken cancellationToken)
    {
        var apiKey = Uri.EscapeDataString(options.Value.ApiKey);
        using var response = await client.GetAsync($"?apikey={apiKey}&type=movie&{query}", cancellationToken);

        // OMDb uses 401 both for an invalid key and for a used-up daily quota. Retrying helps with neither.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new MovieInfoUnavailableException("OMDb refused the request.", refused: true);
        if ((int)response.StatusCode is >= 400 and < 500)
            throw new MovieInfoUnavailableException($"OMDb returned {(int)response.StatusCode}.");
        response.EnsureSuccessStatusCode();

        try
        {
            var movie = await response.Content.ReadFromJsonAsync<OmdbApiResponse>(cancellationToken);
            if (movie?.Response == "True") return movie;
            if (movie is { Response: "False", Error: "Movie not found!" }) return null;

            throw new MovieInfoUnavailableException("OMDb returned an unexpected response.");
        }
        catch (JsonException ex)
        {
            throw new MovieInfoUnavailableException("OMDb returned invalid data.", inner: ex);
        }
    }
}
