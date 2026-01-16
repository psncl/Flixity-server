using server.Features.Movies;

namespace server;

public static class ConfigureServices
{
    extension(WebApplicationBuilder builder)
    {
        public void AddServices()
        {
            builder.AddOmdbClient();
        }

        private void AddOmdbClient()
        {
            builder.Services.AddHttpClient<IMovieInfoClient, MovieInfoOmdbApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.omdbapi.com/");
            });
        }
    }
}