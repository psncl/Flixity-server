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

        var movies = await db.FilmingLocations
            .Where(location => location.CityId == city.Id)
            .OrderBy(location => location.Position)
            .Select(location => new SearchCityMoviesResponse.MovieDetails(location.Movie.ImdbId, location.Movie.Title,
                location.Movie.Year, location.Movie.Director, location.Movie.PosterUrl, location.Movie.Plot,
                location.Blurb))
            .ToListAsync(cancellationToken);

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

    public async Task SaveAsync(CityInfo city, IReadOnlyList<FoundMovie> movies, CancellationToken cancellationToken)
    {
        try
        {
            await TrySaveAsync(city, movies, cancellationToken);
        }
        // Another search saved the same Movie or City since we checked. It exists now, so trying again reuses it.
        catch (Exception ex) when (IsConflict(ex))
        {
            db.ChangeTracker.Clear();
            await TrySaveAsync(city, movies, cancellationToken);
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
        CancellationToken cancellationToken)
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
            var movie = existing.GetValueOrDefault(info.ImdbId) ?? new Movie
            {
                ImdbId = info.ImdbId, Title = info.Title, Year = info.Year, Director = info.Director,
                Plot = info.Plot, PosterUrl = info.PosterUrl, FetchedAt = now
            };
            db.FilmingLocations.Add(new FilmingLocation
                { City = city, Movie = movie, Blurb = blurb, Position = position });
        }

        // EF Core saves everything in one transaction.
        await db.SaveChangesAsync(cancellationToken);
    }
}
