using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
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
                    Icao = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Day = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Inbound = table.Column<int>(type: "INTEGER", nullable: false),
                    Outbound = table.Column<int>(type: "INTEGER", nullable: false),
                    Overflight = table.Column<int>(type: "INTEGER", nullable: false),
                    CoveredMovements = table.Column<int>(type: "INTEGER", nullable: false),
                    AtcMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportDayTraffic", x => new { x.Icao, x.Day });
                });

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
