using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AeroportiMilitari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ElevationFt",
                table: "Airports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMilitaryPresence",
                table: "Airports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Iata",
                table: "Airports",
                type: "TEXT",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMilitaryOnly",
                table: "Airports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "MagneticVariation",
                table: "Airports",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElevationFt",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "HasMilitaryPresence",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "Iata",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "IsMilitaryOnly",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "MagneticVariation",
                table: "Airports");
        }
    }
}
