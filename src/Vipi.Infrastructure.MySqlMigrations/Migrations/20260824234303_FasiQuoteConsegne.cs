using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExitAltitudeFt",
                table: "AtcSessionTraffic",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstPhase",
                table: "AtcSessionTraffic",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "HandoffFromSessionId",
                table: "AtcSessionTraffic",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HandoffToSessionId",
                table: "AtcSessionTraffic",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPhase",
                table: "AtcSessionTraffic",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "MaxAltitudeFt",
                table: "AtcSessionTraffic",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SawAirborne",
                table: "AtcSessionTraffic",
                type: "tinyint(1)",
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
