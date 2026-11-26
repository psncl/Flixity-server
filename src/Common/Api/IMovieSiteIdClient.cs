namespace server.Common.Api;

/// <summary>A Movie's IDs on other sites. Null when the source doesn't know it.</summary>
/// <param name="RottenTomatoesId">Such as "m/fight_club".</param>
/// <param name="MetacriticId">Such as "movie/fight-club".</param>
/// <param name="LetterboxdId">Such as "fight-club".</param>
/// <param name="MubiId">Such as "918".</param>
public record MovieSiteIds(string? RottenTomatoesId, string? MetacriticId, string? LetterboxdId, string? MubiId);

public interface IMovieSiteIdClient
{
    /// <summary>Find the site IDs of several Movies in one request.</summary>
    /// <param name="imdbIds">IMDb IDs, such as "tt0137523". IDs in any other format are skipped.</param>
    /// <param name="cancellationToken">Cancels the lookup</param>
    /// <returns>
    /// Site IDs by IMDb ID. Movies the source doesn't know are left out. Empty if the source fails,
    /// because missing IDs only mean search links: it never throws except when cancelled.
    /// </returns>
    public Task<IReadOnlyDictionary<string, MovieSiteIds>> FindSiteIdsAsync(IReadOnlyCollection<string> imdbIds,
        CancellationToken cancellationToken = default);
}
