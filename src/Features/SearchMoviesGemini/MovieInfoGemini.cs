using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Type = Google.GenAI.Types.Type;

namespace server.Features.Movies;

public class MovieInfoGemini : IMovieInfoLlmClient
{
    private readonly string? _apiKey;

    public MovieInfoGemini(IConfiguration configuration)
    {
        _apiKey = configuration["GEMINI_API_KEY"];
    }

    private static readonly Schema MovieInfoLlmSchema = new()
    {
        Title = "MovieInfo",
        Type = Type.OBJECT,
        Properties = new Dictionary<string, Schema>
        {
            {
                "title", new Schema { Type = Type.STRING, Title = "English title of the movie" }
            },
            {
                "year", new Schema { Type = Type.INTEGER, Title = "Release year of the movie" }
            }
        },
        PropertyOrdering = new List<string> { "title", "year" },
        Required = new List<string> { "title", "year" }
    };

    private static readonly Schema MovieList = new()
    {
        Title = "MovieList",
        Type = Type.ARRAY,
        Items = MovieInfoLlmSchema
    };

    public async Task<List<MovieInfoLlmModel>?> GetMovieListGeminiAsync(string location)
    {
        var client = new Client(apiKey: _apiKey);

        try
        {
            var response = await client.Models.GenerateContentAsync(
                model: "gemini-2.5-flash",
                contents: $"Find 20 movies shot or set in the city {location}.",
                config: new GenerateContentConfig
                {
                    ResponseMimeType = "application/json",
                    ResponseSchema = MovieList
                }
            );

            string? responseText = response.Candidates?[0].Content?.Parts?[0].Text;

            if (responseText is null) return null;

            return JsonSerializer.Deserialize<List<MovieInfoLlmModel>>(responseText);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"An error occurred with Gemini API: {ex}");
        }

        return null;
    }
}