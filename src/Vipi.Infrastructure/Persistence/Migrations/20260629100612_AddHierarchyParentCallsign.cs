using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHierarchyParentCallsign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProjected",
                table: "Sectors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParentCallsign",
                table: "AirportSectors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentCallsign",
                table: "Airports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentCallsign",
                table: "AccSectors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AirportSectors_ParentCallsign",
                table: "AirportSectors",
                column: "ParentCallsign");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_ParentCallsign",
                table: "Airports",
                column: "ParentCallsign");

            migrationBuilder.CreateIndex(
                name: "IX_AccSectors_ParentCallsign",
                table: "AccSectors",
                column: "ParentCallsign");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AirportSectors_ParentCallsign",
                table: "AirportSectors");

            migrationBuilder.DropIndex(
                name: "IX_Airports_ParentCallsign",
                table: "Airports");

            migrationBuilder.DropIndex(
                name: "IX_AccSectors_ParentCallsign",
                table: "AccSectors");

            migrationBuilder.DropColumn(
                name: "IsProjected",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "ParentCallsign",
                table: "AirportSectors");

            migrationBuilder.DropColumn(
                name: "ParentCallsign",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "ParentCallsign",
                table: "AccSectors");
        }
    }
}
