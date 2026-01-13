namespace server.Features.SearchMoviesOmdb;

public class SearchMoviesOmdbApiClient : IGetMovieInfoClient
{
    private readonly HttpClient _client;
    private readonly string? _apiKey;

    private record OmdbApiResponse(
        string Title,
        string Year,
        string Director,
        string imdbID,
        string Plot,
        string Response
    );

    public SearchMoviesOmdbApiClient(HttpClient client, IConfiguration configuration)
    {
        _client = client;
        _apiKey = configuration["OMDB_API_KEY"];
    }

    public async Task<MovieInfo?> GetMovieByTitleYearAsync(string title, int year)
    {
        var response = await _client.GetAsync($"?apikey={_apiKey}&t={title}&y={year}&type=movie");
        response.EnsureSuccessStatusCode();
        var omdbResponse = await response.Content.ReadFromJsonAsync<OmdbApiResponse>();

        // Omdb API returns {"Response":"False","Error":"Movie not found!"} if the search failed
        if (omdbResponse == null || omdbResponse.Response == "False") return null;

        return new MovieInfo(omdbResponse.imdbID, omdbResponse.Title, Convert.ToInt32(omdbResponse.Year),
            omdbResponse.Director, omdbResponse.Plot);
    }
}