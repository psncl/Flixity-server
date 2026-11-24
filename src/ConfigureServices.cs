using Google.GenAI;
using Microsoft.EntityFrameworkCore;
using server.Common.Database;
using server.Features.Cities;
using server.Features.Movies;

namespace server;

public static class ConfigureServices
{
    extension(WebApplicationBuilder builder)
    {
        public void AddServices()
        {
            builder.AddDatabase();
            builder.AddGeocoder();
            builder.AddOmdbClient();
            builder.AddGeminiClient();
        }

        private void AddGeocoder()
        {
            builder.Services.AddHttpClient<ICityGeocoder, CityGeocoderOpenMeteo>(client =>
            {
                client.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/");
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }

        private void AddDatabase()
        {
            builder.Services.AddDbContext<AppDbContext>((services, options) =>
            {
                var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Database")
                                       ?? throw new InvalidOperationException(
                                           "Connection string 'Database' is missing. See README.md.");

                // The production database allows few connections, so keep the pool small by default.
                // The last value wins, so the connection string can still override it.
                options.UseNpgsql($"Maximum Pool Size=10;{connectionString}");
            });
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