using System.Text.Json;
using System.Text.Json.Serialization;

namespace server.Features.Cities;

public class CityGeocoderOpenMeteo(HttpClient client) : ICityGeocoder
{
    private const int MaxCities = 10;

    // Ask for more than we return, because some results are filtered out below.
    private const int RequestedResults = 50;

    // Abandoned, historical, historical capital, destroyed, and section of a city
    private static readonly HashSet<string> ExcludedCodes = ["PPLQ", "PPLH", "PPLCH", "PPLW", "PPLX"];

    private record Response([property: JsonPropertyName("results")] List<Place?>? Results);

    private record Place(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("admin1")] string? Region,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("country_code")] string? CountryCode,
        [property: JsonPropertyName("feature_code")] string? FeatureCode
    );

    public async Task<IReadOnlyList<CityInfo>> SearchAsync(string name, string language,
        CancellationToken cancellationToken = default)
    {
        var url = $"v1/search?name={Uri.EscapeDataString(name)}&count={RequestedResults}" +
                  $"&language={Uri.EscapeDataString(language)}&format=json";

        var response = await GetAsync(url, cancellationToken);

        // Open-Meteo leaves out "results" completely when nothing matches.
        return (response.Results ?? [])
            .OfType<Place>() // Drops nulls, which JSON lists can contain
            .Where(place => IsCity(place.FeatureCode))
            .Where(place => !string.IsNullOrEmpty(place.Name) && !string.IsNullOrEmpty(place.CountryCode))
            .Take(MaxCities)
            .Select(place => new CityInfo(place.Id, place.Name!, place.Region,
                place.Country ?? place.CountryCode!, place.CountryCode!))
            .ToList();
    }

    // GeoNames codes for populated places start with PPL. Others are regions, mountains etc.
    private static bool IsCity(string? featureCode) =>
        featureCode?.StartsWith("PPL", StringComparison.Ordinal) == true && !ExcludedCodes.Contains(featureCode);

    private async Task<Response> GetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode(); // Quota errors arrive here as 429

            return await response.Content.ReadFromJsonAsync<Response>(cancellationToken)
                   ?? throw new GeocoderUnavailableException("The geocoder returned an empty response.");
        }
        // A timeout also throws OperationCanceledException, but then the caller hasn't cancelled.
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new GeocoderUnavailableException("The geocoder request failed.", ex);
        }
    }
}
