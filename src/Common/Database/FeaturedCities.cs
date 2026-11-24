namespace server.Common.Database;

/// <summary>
/// The curated UK and European Cities, seeded through migrations. Names and GeoNames IDs are from Open-Meteo.
/// </summary>
/// <remarks>
/// Before adding a City: a user may already have searched it, so its row may exist under another Id. Then the
/// generated InsertData would break the unique GeoNamesId. Replace it in the new migration with SQL like
/// INSERT ... ON CONFLICT ("GeoNamesId") DO UPDATE SET "IsFeatured" = true.
/// </remarks>
public static class FeaturedCities
{
    public static readonly City[] All =
    [
        Create(1, 2643743, "London", "England", "United Kingdom", "GB"),
        Create(2, 2650225, "Edinburgh", "Scotland", "United Kingdom", "GB"),
        Create(3, 2648579, "Glasgow", "Scotland", "United Kingdom", "GB"),
        Create(4, 2643123, "Manchester", "England", "United Kingdom", "GB"),
        Create(5, 2644210, "Liverpool", "England", "United Kingdom", "GB"),
        Create(6, 2656173, "Bath", "England", "United Kingdom", "GB"),
        Create(7, 2640729, "Oxford", "England", "United Kingdom", "GB"),
        Create(8, 2655984, "Belfast", "Northern Ireland", "United Kingdom", "GB"),
        Create(9, 2964574, "Dublin", "Leinster", "Ireland", "IE"),
        Create(10, 2988507, "Paris", "Île-de-France Region", "France", "FR"),
        Create(11, 2990440, "Nice", "Provence-Alpes-Côte d'Azur", "France", "FR"),
        Create(12, 3169070, "Rome", "Lazio", "Italy", "IT"),
        Create(13, 3164603, "Venice", "Veneto", "Italy", "IT"),
        Create(14, 3176959, "Florence", "Tuscany", "Italy", "IT"),
        Create(15, 3117735, "Madrid", "Madrid", "Spain", "ES"),
        Create(16, 3128760, "Barcelona", "Catalonia", "Spain", "ES"),
        Create(17, 2510911, "Seville", "Andalusia", "Spain", "ES"),
        Create(18, 2950159, "Berlin", "State of Berlin", "Germany", "DE"),
        Create(19, 2867714, "Munich", "Bavaria", "Germany", "DE"),
        Create(20, 2267057, "Lisbon", "Lisbon District", "Portugal", "PT"),
        // Open-Meteo says "The Netherlands", which would sort under T.
        Create(21, 2759794, "Amsterdam", "North Holland", "Netherlands", "NL"),
        Create(22, 2800931, "Bruges", "Flanders", "Belgium", "BE"),
        Create(23, 2761369, "Vienna", "State of Vienna", "Austria", "AT"),
        Create(24, 2766824, "Salzburg", "State of Salzburg", "Austria", "AT"),
        Create(25, 3067696, "Prague", "Prague", "Czechia", "CZ"),
        Create(26, 3054643, "Budapest", "Budapest", "Hungary", "HU"),
        Create(27, 2618425, "Copenhagen", "Capital Region", "Denmark", "DK"),
        Create(28, 2673730, "Stockholm", "Stockholm County", "Sweden", "SE"),
        Create(29, 264371, "Athens", "Attica", "Greece", "GR"),
        Create(30, 3201047, "Dubrovnik", "Dubrovnik-Neretva County", "Croatia", "HR")
    ];

    // Seed rows need fixed primary keys, so that later migrations can find them.
    // They are negative because the database generates positive ones, so the two can never clash.
    private static City Create(int id, int geoNamesId, string name, string region, string country,
        string countryCode) => new()
    {
        Id = -id, GeoNamesId = geoNamesId, Name = name, Region = region, Country = country,
        CountryCode = countryCode, IsFeatured = true
    };
}
