namespace server.Common.Api;

public record CityInfo(int GeoNamesId, string Name, string? Region, string Country, string CountryCode);

/// <summary>The geocoder could not answer. Different from an answer with no matches.</summary>
public class GeocoderUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public interface ICityGeocoder
{
    /// <summary>
    /// Find Cities matching a name.
    /// </summary>
    /// <param name="name">Full or partial City name</param>
    /// <param name="language">Two-letter language code used for matching and for the returned names</param>
    /// <param name="cancellationToken">Cancels the search</param>
    /// <returns>Up to 10 Cities, or an empty list if nothing matches.</returns>
    /// <exception cref="GeocoderUnavailableException">The geocoder failed, timed out or refused the request.</exception>
    public Task<IReadOnlyList<CityInfo>> SearchAsync(string name, string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get one City, with its names in English.
    /// </summary>
    /// <returns>The City, or null if the ID is unknown or is not a City.</returns>
    /// <exception cref="GeocoderUnavailableException">The geocoder failed, timed out or refused the request.</exception>
    public Task<CityInfo?> GetByIdAsync(int geoNamesId, CancellationToken cancellationToken = default);
}
