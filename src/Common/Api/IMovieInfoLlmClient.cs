using server.Features.Movies;

namespace server.Common.Api;

public interface IMovieInfoLlmClient
{
    public Task<List<MovieInfoGemini.MovieInfoModel>?> GetMovieListGeminiAsync(string location);
}