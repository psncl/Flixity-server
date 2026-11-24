using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
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
            builder.AddLlmClient();
            builder.AddCityMovieSearch();
        }

        private void AddCityMovieSearch()
        {
            builder.Services.AddScoped<CityMovieStore>();
            builder.Services.AddScoped<CityMovieFinder>();
            // Shared by all requests
            builder.Services.AddSingleton<CitySearchLocks>();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<UncachedSearchLimiter>();
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
            builder.Services.AddOptions<OmdbOptions>().Configure<IConfiguration>((options, configuration) =>
                options.ApiKey = configuration["OMDB_API_KEY"] ?? "");

            builder.Services.AddHttpClient<IMovieInfoClient, MovieInfoOmdbApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.omdbapi.com/");
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }

        private void AddLlmClient()
        {
            builder.Services.AddOptions<LlmOptions>()
                .BindConfiguration("Llm")
                .Validate(options => options.Provider == LlmOptions.OpenAi,
                    $"Llm:Provider is not supported. Supported providers: {LlmOptions.OpenAi}.")
                .Validate(options => options.Model != "", "Llm:Model is missing.")
                .ValidateOnStart();

            // To add a provider, choose its IChatClient here by options.Provider.
            // One client is shared, because each one can own network connections.
            builder.Services.AddSingleton<IChatClient>(services =>
            {
                var options = services.GetRequiredService<IOptions<LlmOptions>>().Value;
                var apiKey = services.GetRequiredService<IConfiguration>()["OPENAI_API_KEY"];
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException("OPENAI_API_KEY is missing. See README.md.");

                var clientOptions = new OpenAIClientOptions
                {
                    // MovieInfoLlmClient does the retries and the timeout.
                    RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
                    NetworkTimeout = Timeout.InfiniteTimeSpan
                };
                return new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
                    .GetChatClient(options.Model)
                    .AsIChatClient();
            });

            // Created when first used, so that cached searches work without an API key.
            builder.Services.AddSingleton(services =>
                new Lazy<IChatClient>(services.GetRequiredService<IChatClient>));
            builder.Services.AddSingleton<IMovieInfoLlmClient, MovieInfoLlmClient>();
        }
    }
}
