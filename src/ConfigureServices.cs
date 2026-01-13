using server.Features.SearchMoviesOmdb;

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
            builder.Services.AddHttpClient<IGetMovieInfoClient, SearchMoviesOmdbApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.omdbapi.com/");
            });
        }
    }
}