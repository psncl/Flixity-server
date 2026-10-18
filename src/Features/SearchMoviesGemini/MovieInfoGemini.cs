using System.Text.Json;
using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;
using Type = Google.GenAI.Types.Type;

namespace server.Features.Movies;

public class MovieInfoGemini : IMovieInfoLlmClient
{
    private readonly Client _client;

    // Client is injected as a singleton: each Client owns its own HttpClient,
    // so creating one per request would leak sockets.
    public MovieInfoGemini(Client client)
    {
        _client = client;
    }

    // private static readonly Schema MovieInfoLlmSchema = new()
    // {
    //     Title = "MovieInfo",
    //     Type = Type.OBJECT,
    //     Properties = new Dictionary<string, Schema>
    //     {
    //         {
    //             "title", new Schema { Type = Type.STRING, Title = "English title of the movie" }
    //         },
    //         {
    //             "year", new Schema { Type = Type.INTEGER, Title = "Release year of the movie" }
    //         }
    //     },
    //     PropertyOrdering = new List<string> { "title", "year" },
    //     Required = new List<string> { "title", "year" }
    // };

    private static string MovieInfoLlmSchemaString = @"
    {
        ""type"": ""object"",
        ""properties"": {
            ""title"": {""type"": ""string"", ""title"": ""English title of the movie""},
            ""year"": {""type"": ""integer"", ""title"": ""Release year of the movie""}
        },
        ""required"": [""title"", ""year""]
    }";

    // private static readonly Schema MovieList = new()
    // {
    //     Title = "MovieList",
    //     Type = Type.ARRAY,
    //     Items = MovieInfoLlmSchema
    // };

    private static string MovieListSchemaString = @"
    {
        ""type"": ""array"",
        ""items"": ""MovieInfoLlmSchemaString""
    }";

    public async Task<List<MovieInfoLlmModel>?> GetMovieListGeminiAsync(string location)
    {
        try
        {
            var response = await _client.Models.GenerateContentAsync(
                model: "gemini-2.5-flash",
                contents: $"Find 20 movies shot or set in the city {location}.",
                config: new GenerateContentConfig
                {
                    ResponseMimeType = "application/json",
                    ResponseJsonSchema = JsonNode.Parse(MovieListSchemaString)
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