using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using server.Common.Database;

namespace server.Features.Cities;

public class GetFeaturedCitiesHandler
{
    public static async Task<Ok<List<CityInfo>>> Handle(AppDbContext db, CancellationToken cancellationToken)
    {
        var cities = await db.Cities
            .Where(city => city.IsFeatured)
            .OrderBy(city => city.Country).ThenBy(city => city.Name)
            .Select(city => new CityInfo(city.GeoNamesId, city.Name, city.Region, city.Country, city.CountryCode))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(cities);
    }
}
