using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class CoordinateSogliaPista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ThresholdElevationFt",
                table: "AirportRunways",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ThresholdLat",
                table: "AirportRunways",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ThresholdLon",
                table: "AirportRunways",
                type: "double",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThresholdElevationFt",
                table: "AirportRunways");

            migrationBuilder.DropColumn(
                name: "ThresholdLat",
                table: "AirportRunways");

            migrationBuilder.DropColumn(
                name: "ThresholdLon",
                table: "AirportRunways");
        }
    }
}
