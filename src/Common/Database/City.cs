namespace server.Common.Database;

public class City
{
    public int Id { get; set; }

    /// <summary>Stable place ID from GeoNames, which tells apart Cities that share a name.</summary>
    public required int GeoNamesId { get; set; }

    public required string Name { get; set; }
    public string? Region { get; set; }
    public required string Country { get; set; }
    public required string CountryCode { get; set; }
    public bool IsFeatured { get; set; }

    /// <summary>When the City's movies were generated. Null means it has never been searched.</summary>
    public DateTimeOffset? GeneratedAt { get; set; }
}
