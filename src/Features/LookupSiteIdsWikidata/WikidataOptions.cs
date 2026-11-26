namespace server.Features.Movies;

public class WikidataOptions
{
    /// <summary>
    /// Wikidata blocks requests without a descriptive User-Agent. Its policy asks for the app's name
    /// and a way to contact its owner: https://foundation.wikimedia.org/wiki/Policy:User-Agent_policy
    /// </summary>
    public string UserAgent { get; set; } = "Flixity/0.1 (movie filming locations app)";
}
