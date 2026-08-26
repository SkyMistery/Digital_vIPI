using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                    Month = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Callsign = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Position = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sessions = table.Column<int>(type: "int", nullable: false),
                    Seconds = table.Column<long>(type: "bigint", nullable: false),
                    TrafficSeen = table.Column<int>(type: "int", nullable: false),
                    TrafficMoved = table.Column<int>(type: "int", nullable: false),
                    BusyMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtcMonthRollups", x => new { x.Month, x.UserId, x.Callsign });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
