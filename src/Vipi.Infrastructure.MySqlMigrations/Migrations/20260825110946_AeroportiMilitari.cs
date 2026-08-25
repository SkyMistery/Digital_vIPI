using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMilitaryPresence",
                table: "Airports",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Iata",
                table: "Airports",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsMilitaryOnly",
                table: "Airports",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "MagneticVariation",
                table: "Airports",
                type: "double",
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
