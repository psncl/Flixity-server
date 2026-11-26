using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Common.Database;

namespace server.Features.Movies;

/// <summary>Saves and reads the completed movie searches of Cities.</summary>
public class CityMovieStore(AppDbContext db)
{
    /// <returns>Null if the City has no completed search.</returns>
    public async Task<SearchCityMoviesResponse?> GetAsync(int geoNamesId, CancellationToken cancellationToken)
    {
        var city = await db.Cities.AsNoTracking()
            .SingleOrDefaultAsync(c => c.GeoNamesId == geoNamesId && c.GeneratedAt != null, cancellationToken);
        if (city is null) return null;

        // Links are built in memory after loading: EF Core can't translate MovieLinks.For to SQL.
        var locations = await db.FilmingLocations.AsNoTracking()
            .Where(location => location.CityId == city.Id)
            .OrderBy(location => location.Position)
            .Select(location => new { location.Movie, location.Blurb })
            .ToListAsync(cancellationToken);
        var movies = locations
            .Select(location => SearchCityMoviesResponse.MovieDetails.From(location.Movie, location.Blurb))
            .ToList();

        return new SearchCityMoviesResponse(
            new SearchCityMoviesResponse.CityDetails(city.GeoNamesId, city.Name, city.Region, city.Country,
                city.CountryCode),
            Partial: false, movies);
    }

    /// <returns>Null if the City is not stored.</returns>
    public Task<CityInfo?> GetCityAsync(int geoNamesId, CancellationToken cancellationToken) =>
        db.Cities
            .Where(city => city.GeoNamesId == geoNamesId)
            .Select(city => new CityInfo(city.GeoNamesId, city.Name, city.Region, city.Country, city.CountryCode))
            .SingleOrDefaultAsync(cancellationToken);

    /// <returns>The Movies among these IMDb IDs that are already stored, by IMDb ID. Read-only.</returns>
    public Task<Dictionary<string, Movie>> GetMoviesAsync(IReadOnlyCollection<string> imdbIds,
        CancellationToken cancellationToken) =>
        db.Movies.AsNoTracking()
            .Where(movie => imdbIds.Contains(movie.ImdbId))
            .ToDictionaryAsync(movie => movie.ImdbId, cancellationToken);

    /// <summary>A Movie as first stored: OMDb's metadata and ratings, and the site IDs if any were found.</summary>
    public static Movie NewMovie(MovieInfo info, MovieSiteIds? siteIds, DateTimeOffset fetchedAt) => new()
    {
        ImdbId = info.ImdbId, Title = info.Title, Year = info.Year, Director = info.Director,
        Plot = info.Plot, PosterUrl = info.PosterUrl, FetchedAt = fetchedAt,
        ImdbRating = info.ImdbRating, Metascore = info.Metascore,
        RottenTomatoesId = siteIds?.RottenTomatoesId, MetacriticId = siteIds?.MetacriticId,
        LetterboxdId = siteIds?.LetterboxdId, MubiId = siteIds?.MubiId,
    };

    /// <param name="siteIds">Site IDs of the new Movies, by IMDb ID. Movies already stored keep theirs.</param>
    public async Task SaveAsync(CityInfo city, IReadOnlyList<FoundMovie> movies,
        IReadOnlyDictionary<string, MovieSiteIds> siteIds, CancellationToken cancellationToken)
    {
        try
        {
            await TrySaveAsync(city, movies, siteIds, cancellationToken);
        }
        // Another search saved the same Movie or City since we checked. It exists now, so trying again reuses it.
        catch (Exception ex) when (IsConflict(ex))
        {
            db.ChangeTracker.Clear();
            await TrySaveAsync(city, movies, siteIds, cancellationToken);
        }
    }

    // EF Core wraps Postgres errors differently, depending on whether they may go away.
    private static bool IsConflict(Exception? ex) => ex switch
    {
        null => false,
        PostgresException postgres => postgres.SqlState is PostgresErrorCodes.UniqueViolation
            or PostgresErrorCodes.DeadlockDetected,
        _ => IsConflict(ex.InnerException)
    };

    private async Task TrySaveAsync(CityInfo cityInfo, IReadOnlyList<FoundMovie> found,
        IReadOnlyDictionary<string, MovieSiteIds> siteIds, CancellationToken cancellationToken)
    {
        var city = await db.Cities.SingleOrDefaultAsync(c => c.GeoNamesId == cityInfo.GeoNamesId, cancellationToken);
        if (city?.GeneratedAt is not null) return; // Another search completed it

        if (city is null)
        {
            city = new City
            {
                GeoNamesId = cityInfo.GeoNamesId, Name = cityInfo.Name, Region = cityInfo.Region,
                Country = cityInfo.Country, CountryCode = cityInfo.CountryCode
            };
            db.Cities.Add(city);
        }

        var now = DateTimeOffset.UtcNow;
        city.GeneratedAt = now;

        var imdbIds = found.Select(f => f.Movie.ImdbId).ToList();
        var existing = await db.Movies.Where(movie => imdbIds.Contains(movie.ImdbId))
            .ToDictionaryAsync(movie => movie.ImdbId, cancellationToken);

        // Every search adds Movies in the same order. Otherwise two searches could add the same Movies in opposite
        // orders, and wait for each other forever (a deadlock). Position keeps the original order.
        foreach (var (position, (info, blurb)) in found.Index()
                     .OrderBy(item => item.Item.Movie.ImdbId, StringComparer.Ordinal))
        {
            // An existing Movie keeps its metadata, ratings and site IDs: they are never refreshed.
            var movie = existing.GetValueOrDefault(info.ImdbId) ??
                        NewMovie(info, siteIds.GetValueOrDefault(info.ImdbId), now);
            db.FilmingLocations.Add(new FilmingLocation
                { City = city, Movie = movie, Blurb = blurb, Position = position });
        }

        // EF Core saves everything in one transaction.
        await db.SaveChangesAsync(cancellationToken);
    }
}
