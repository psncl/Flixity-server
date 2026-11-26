using Microsoft.EntityFrameworkCore;

namespace server.Common.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<FilmingLocation> FilmingLocations => Set<FilmingLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<City>().HasIndex(city => city.GeoNamesId).IsUnique();
        modelBuilder.Entity<City>().HasData(FeaturedCities.All);

        modelBuilder.Entity<Movie>().HasIndex(movie => movie.ImdbId).IsUnique();
        // At most 10.0, with one decimal place.
        modelBuilder.Entity<Movie>().Property(movie => movie.ImdbRating).HasPrecision(3, 1);

        // The foreign keys are required, so EF Core deletes Filming Locations with their City or Movie.
        modelBuilder.Entity<FilmingLocation>().HasKey(location => new { location.CityId, location.MovieId });
    }
}
