using System.Net;
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

    private record SearchResponse([property: JsonPropertyName("results")] List<Place?>? Results);

    private record ErrorResponse([property: JsonPropertyName("reason")] string? Reason);

    private record Place(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("admin1")] string? Region,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("country_code")] string? CountryCode,
        [property: JsonPropertyName("feature_code")] string? FeatureCode
    );

    public Task<IReadOnlyList<CityInfo>> SearchAsync(string name, string language,
        CancellationToken cancellationToken = default)
    {
        var url = $"v1/search?name={Uri.EscapeDataString(name)}&count={RequestedResults}" +
                  $"&language={Uri.EscapeDataString(language)}&format=json";

        return SendAsync<IReadOnlyList<CityInfo>>(url, async response =>
        {
            response.EnsureSuccessStatusCode(); // Quota errors arrive here as 429
            var body = await ReadAsync<SearchResponse>(response, cancellationToken);

            // Open-Meteo leaves out "results" completely when nothing matches.
            return (body.Results ?? []).Select(ToCity).OfType<CityInfo>().Take(MaxCities).ToList();
        }, cancellationToken);
    }

    public Task<CityInfo?> GetByIdAsync(int geoNamesId, CancellationToken cancellationToken = default)
    {
        return SendAsync($"v1/get?id={geoNamesId}&language=en&format=json", async response =>
        {
            // Open-Meteo reports an unknown ID as 400, like any other invalid request.
            if (response.StatusCode == HttpStatusCode.BadRequest &&
                (await ReadAsync<ErrorResponse>(response, cancellationToken)).Reason == "Location ID not found.")
                return null;

            response.EnsureSuccessStatusCode();
            var place = await ReadAsync<Place>(response, cancellationToken);

            // Otherwise the City would be saved under an ID that nobody searches for.
            if (place.Id != geoNamesId)
                throw new GeocoderUnavailableException("The geocoder returned a different place.");

            return ToCity(place);
        }, cancellationToken);
    }

    /// <returns>Null if the place is not a City.</returns>
    private static CityInfo? ToCity(Place? place)
    {
        // GeoNames codes for populated places start with PPL. Others are regions, mountains etc.
        if (place?.FeatureCode?.StartsWith("PPL", StringComparison.Ordinal) != true ||
            ExcludedCodes.Contains(place.FeatureCode)) return null;

        if (string.IsNullOrEmpty(place.Name) || string.IsNullOrEmpty(place.CountryCode)) return null;

        return new CityInfo(place.Id, place.Name, place.Region, place.Country ?? place.CountryCode, place.CountryCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken)
        ?? throw new GeocoderUnavailableException("The geocoder returned an empty response.");

    private async Task<T> SendAsync<T>(string url, Func<HttpResponseMessage, Task<T>> read,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            return await read(response);
        }
        // A timeout also throws OperationCanceledException, but then the caller hasn't cancelled.
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new GeocoderUnavailableException("The geocoder request failed.", ex);
        }
    }
}
