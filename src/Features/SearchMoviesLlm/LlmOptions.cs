namespace server.Features.Movies;

public class LlmOptions
{
    public const string OpenAi = "OpenAI";

    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}
