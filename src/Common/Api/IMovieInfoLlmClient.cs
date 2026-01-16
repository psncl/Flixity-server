using System.Text.Json.Serialization;

namespace server.Common.Api;

public record MovieInfoLlmModel
{
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("year")] public int Year { get; set; }
}

public interface IMovieInfoLlmClient
{
    public Task<List<MovieInfoLlmModel>?> GetMovieListGeminiAsync(string location);
}