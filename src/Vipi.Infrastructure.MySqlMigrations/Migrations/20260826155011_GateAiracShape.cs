using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class GateAiracShape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegionMapPolygonInForce",
                table: "AirportSectors",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ShapeAiracCycle",
                table: "AirportSectors",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "ShapeForcePublished",
                table: "AirportSectors",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShapeSource",
                table: "AirportSectors",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Source",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RegionMapPolygonInForce",
                table: "AccSectors",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ShapeAiracCycle",
                table: "AccSectors",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "ShapeForcePublished",
                table: "AccSectors",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShapeSource",
                table: "AccSectors",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Source",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegionMapPolygonInForce",
                table: "AirportSectors");

            migrationBuilder.DropColumn(
                name: "ShapeAiracCycle",
                table: "AirportSectors");

            migrationBuilder.DropColumn(
                name: "ShapeForcePublished",
                table: "AirportSectors");

            migrationBuilder.DropColumn(
                name: "ShapeSource",
                table: "AirportSectors");

            migrationBuilder.DropColumn(
                name: "RegionMapPolygonInForce",
                table: "AccSectors");

            migrationBuilder.DropColumn(
                name: "ShapeAiracCycle",
                table: "AccSectors");

            migrationBuilder.DropColumn(
                name: "ShapeForcePublished",
                table: "AccSectors");

            migrationBuilder.DropColumn(
                name: "ShapeSource",
                table: "AccSectors");
        }
    }
}
