using Google.GenAI;
using server.Features.Movies;

namespace server;

public static class ConfigureServices
{
    extension(WebApplicationBuilder builder)
    {
        public void AddServices()
        {
            builder.AddOmdbClient();
            builder.AddGeminiClient();
        }

        private void AddOmdbClient()
        {
            builder.Services.AddHttpClient<IMovieInfoClient, MovieInfoOmdbApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.omdbapi.com/");
            });
        }

        private void AddGeminiClient()
        {
            builder.Services.AddSingleton(_ => new Client(apiKey: builder.Configuration["GEMINI_API_KEY"]));
            builder.Services.AddSingleton<IMovieInfoLlmClient, MovieInfoGemini>();
        }
    }
}