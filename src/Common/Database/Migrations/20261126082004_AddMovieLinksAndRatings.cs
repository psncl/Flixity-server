using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Common.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieLinksAndRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Saved results from before this change have no links or ratings, and saved results are
            // never refreshed. Nothing is deployed yet, so delete them and let the next search
            // generate them again. Cities are kept; resetting GeneratedAt marks them as never searched.
            // Down does not restore the deleted data.
            migrationBuilder.Sql("""
                DELETE FROM "FilmingLocations";
                DELETE FROM "Movies";
                UPDATE "Cities" SET "GeneratedAt" = NULL;
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "ImdbRating",
                table: "Movies",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LetterboxdId",
                table: "Movies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetacriticId",
                table: "Movies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Metascore",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MubiId",
                table: "Movies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RottenTomatoes",
                table: "Movies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RottenTomatoesId",
                table: "Movies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImdbRating",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "LetterboxdId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "MetacriticId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "Metascore",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "MubiId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "RottenTomatoes",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "RottenTomatoesId",
                table: "Movies");
        }
    }
}
