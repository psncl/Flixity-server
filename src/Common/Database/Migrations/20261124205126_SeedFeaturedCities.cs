using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace server.Common.Database.Migrations
{
    /// <inheritdoc />
    public partial class SeedFeaturedCities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Cities",
                columns: new[] { "Id", "Country", "CountryCode", "GeneratedAt", "GeoNamesId", "IsFeatured", "Name", "Region" },
                values: new object[,]
                {
                    { -30, "Croatia", "HR", null, 3201047, true, "Dubrovnik", "Dubrovnik-Neretva County" },
                    { -29, "Greece", "GR", null, 264371, true, "Athens", "Attica" },
                    { -28, "Sweden", "SE", null, 2673730, true, "Stockholm", "Stockholm County" },
                    { -27, "Denmark", "DK", null, 2618425, true, "Copenhagen", "Capital Region" },
                    { -26, "Hungary", "HU", null, 3054643, true, "Budapest", "Budapest" },
                    { -25, "Czechia", "CZ", null, 3067696, true, "Prague", "Prague" },
                    { -24, "Austria", "AT", null, 2766824, true, "Salzburg", "State of Salzburg" },
                    { -23, "Austria", "AT", null, 2761369, true, "Vienna", "State of Vienna" },
                    { -22, "Belgium", "BE", null, 2800931, true, "Bruges", "Flanders" },
                    { -21, "Netherlands", "NL", null, 2759794, true, "Amsterdam", "North Holland" },
                    { -20, "Portugal", "PT", null, 2267057, true, "Lisbon", "Lisbon District" },
                    { -19, "Germany", "DE", null, 2867714, true, "Munich", "Bavaria" },
                    { -18, "Germany", "DE", null, 2950159, true, "Berlin", "State of Berlin" },
                    { -17, "Spain", "ES", null, 2510911, true, "Seville", "Andalusia" },
                    { -16, "Spain", "ES", null, 3128760, true, "Barcelona", "Catalonia" },
                    { -15, "Spain", "ES", null, 3117735, true, "Madrid", "Madrid" },
                    { -14, "Italy", "IT", null, 3176959, true, "Florence", "Tuscany" },
                    { -13, "Italy", "IT", null, 3164603, true, "Venice", "Veneto" },
                    { -12, "Italy", "IT", null, 3169070, true, "Rome", "Lazio" },
                    { -11, "France", "FR", null, 2990440, true, "Nice", "Provence-Alpes-Côte d'Azur" },
                    { -10, "France", "FR", null, 2988507, true, "Paris", "Île-de-France Region" },
                    { -9, "Ireland", "IE", null, 2964574, true, "Dublin", "Leinster" },
                    { -8, "United Kingdom", "GB", null, 2655984, true, "Belfast", "Northern Ireland" },
                    { -7, "United Kingdom", "GB", null, 2640729, true, "Oxford", "England" },
                    { -6, "United Kingdom", "GB", null, 2656173, true, "Bath", "England" },
                    { -5, "United Kingdom", "GB", null, 2644210, true, "Liverpool", "England" },
                    { -4, "United Kingdom", "GB", null, 2643123, true, "Manchester", "England" },
                    { -3, "United Kingdom", "GB", null, 2648579, true, "Glasgow", "Scotland" },
                    { -2, "United Kingdom", "GB", null, 2650225, true, "Edinburgh", "Scotland" },
                    { -1, "United Kingdom", "GB", null, 2643743, true, "London", "England" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -30);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -29);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -28);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -27);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -26);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -25);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -24);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -23);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -22);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -21);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -20);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -19);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -18);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -17);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -16);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -15);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -14);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -13);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -12);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -11);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -10);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -9);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -8);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -7);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -6);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -5);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -4);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -3);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -2);

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValue: -1);
        }
    }
}
