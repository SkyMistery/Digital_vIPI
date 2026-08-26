using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShapeAiracCycle",
                table: "AirportSectors",
                type: "TEXT",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShapeForcePublished",
                table: "AirportSectors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShapeSource",
                table: "AirportSectors",
                type: "TEXT",
                nullable: false,
                defaultValue: "Source");

            migrationBuilder.AddColumn<string>(
                name: "RegionMapPolygonInForce",
                table: "AccSectors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShapeAiracCycle",
                table: "AccSectors",
                type: "TEXT",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShapeForcePublished",
                table: "AccSectors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShapeSource",
                table: "AccSectors",
                type: "TEXT",
                nullable: false,
                defaultValue: "Source");
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
