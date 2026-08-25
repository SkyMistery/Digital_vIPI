using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class TrafficoAeroportoGiornaliero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AirportDayTraffic",
                columns: table => new
                {
                    Icao = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Day = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Inbound = table.Column<int>(type: "int", nullable: false),
                    Outbound = table.Column<int>(type: "int", nullable: false),
                    Overflight = table.Column<int>(type: "int", nullable: false),
                    CoveredMovements = table.Column<int>(type: "int", nullable: false),
                    AtcMinutes = table.Column<int>(type: "int", nullable: false),
                    FetchedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportDayTraffic", x => new { x.Icao, x.Day });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AirportDayTraffic_Day",
                table: "AirportDayTraffic",
                column: "Day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirportDayTraffic");
        }
    }
}
