using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Common.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropRottenTomatoesScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RottenTomatoes",
                table: "Movies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RottenTomatoes",
                table: "Movies",
                type: "integer",
                nullable: true);
        }
    }
}
