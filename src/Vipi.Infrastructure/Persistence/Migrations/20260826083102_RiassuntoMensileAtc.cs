using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiassuntoMensileAtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtcMonthRollups",
                columns: table => new
                {
                    Month = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Sessions = table.Column<int>(type: "INTEGER", nullable: false),
                    Seconds = table.Column<long>(type: "INTEGER", nullable: false),
                    TrafficSeen = table.Column<int>(type: "INTEGER", nullable: false),
                    TrafficMoved = table.Column<int>(type: "INTEGER", nullable: false),
                    BusyMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtcMonthRollups", x => new { x.Month, x.UserId, x.Callsign });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AtcMonthRollups_Month",
                table: "AtcMonthRollups",
                column: "Month");

            migrationBuilder.CreateIndex(
                name: "IX_AtcMonthRollups_UserId",
                table: "AtcMonthRollups",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtcMonthRollups");
        }
    }
}
