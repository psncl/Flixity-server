using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace server.Features.Cities;

public class SearchCitiesHandler
{
    private const string DefaultLanguage = "en";

    // Binding rejects a missing query (it is not nullable). The attributes reject an empty one or a wrong length.
    public static async Task<Results<Ok<IReadOnlyList<CityInfo>>, ProblemHttpResult>> Handle(
        [FromQuery, Required, TrimmedLength(2, 100)] string query,
        HttpRequest request, ICityGeocoder geocoder, ILogger<SearchCitiesHandler> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var cities = await geocoder.SearchAsync(query.Trim(), GetLanguage(request), cancellationToken);
            return TypedResults.Ok(cities);
        }
        catch (GeocoderUnavailableException ex)
        {
            logger.LogError(ex, "City suggestions failed because the geocoder is unavailable");
            return TypedResults.Problem(
                title: "City search is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>The browser's preferred language, e.g. "fr" from "fr-CA,fr;q=0.9,en;q=0.8".</summary>
    private static string GetLanguage(HttpRequest request)
    {
        var preferred = request.GetTypedHeaders().AcceptLanguage
            .Where(language => language.Quality != 0)
            .OrderByDescending(language => language.Quality ?? 1)
            .Select(language => language.Value.Value)
            .FirstOrDefault();

        return preferred is null or "*" ? DefaultLanguage : preferred.Split('-')[0].ToLowerInvariant();
    }
}
