using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAtcCallsign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtcCallsign",
                table: "AirportSectors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtcCallsign",
                table: "AccSectors",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtcCallsign",
                table: "AirportSectors");

            migrationBuilder.DropColumn(
                name: "AtcCallsign",
                table: "AccSectors");
        }
    }
}
