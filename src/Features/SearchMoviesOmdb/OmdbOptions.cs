namespace server.Features.Movies;

public class OmdbOptions
{
    public string ApiKey { get; set; } = "";

    /// <summary>How long to wait before each retry. The number of items is the number of retries.</summary>
    public TimeSpan[] RetryDelays { get; set; } = [TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1)];
}
