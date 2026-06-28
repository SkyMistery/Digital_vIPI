using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeaturedRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeaturedRank",
                table: "Sectors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeaturedRank",
                table: "Airports",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeaturedRank",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "FeaturedRank",
                table: "Airports");
        }
    }
}
