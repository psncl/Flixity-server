using Microsoft.EntityFrameworkCore;

namespace server.Common.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<City>().HasIndex(city => city.GeoNamesId).IsUnique();
        modelBuilder.Entity<City>().HasData(FeaturedCities.All);
    }
}
