using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FasiQuoteConsegne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntryAltitudeFt",
                table: "AtcSessionTraffic",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExitAltitudeFt",
                table: "AtcSessionTraffic",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstPhase",
                table: "AtcSessionTraffic",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HandoffFromSessionId",
                table: "AtcSessionTraffic",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HandoffToSessionId",
                table: "AtcSessionTraffic",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPhase",
                table: "AtcSessionTraffic",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAltitudeFt",
                table: "AtcSessionTraffic",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SawAirborne",
                table: "AtcSessionTraffic",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntryAltitudeFt",
                table: "AtcSessionTraffic");

            migrationBuilder.DropColumn(
                name: "ExitAltitudeFt",
                table: "AtcSessionTraffic");

            migrationBuilder.DropColumn(
                name: "FirstPhase",
                table: "AtcSessionTraffic");

            migrationBuilder.DropColumn(
                name: "HandoffFromSessionId",
                table: "AtcSessionTraffic");

            migrationBuilder.DropColumn(
                name: "HandoffToSessionId",
                table: "AtcSessionTraffic");

            migrationBuilder.DropColumn(
                name: "LastPhase",
                table: "AtcSessionTraffic");

            migrationBuilder.DropColumn(
                name: "MaxAltitudeFt",
                table: "AtcSessionTraffic");

            migrationBuilder.DropColumn(
                name: "SawAirborne",
                table: "AtcSessionTraffic");
        }
    }
}
