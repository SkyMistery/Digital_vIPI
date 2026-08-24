using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class StatisticheAtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtcSessions",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Callsign = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Position = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Frequency = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShiftKey = table.Column<long>(type: "bigint", nullable: false),
                    TrafficCount = table.Column<int>(type: "int", nullable: false),
                    MovementCount = table.Column<int>(type: "int", nullable: false),
                    TrafficMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtcSessions", x => x.SessionId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AtcSessionTraffic",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    PilotCallsign = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LegOrdinal = table.Column<int>(type: "int", nullable: false),
                    PilotUserId = table.Column<int>(type: "int", nullable: false),
                    FlightPlanId = table.Column<long>(type: "bigint", nullable: true),
                    DepIcao = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArrIcao = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AircraftIcao = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SeenMinutes = table.Column<int>(type: "int", nullable: false),
                    SawMovement = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasObservationGap = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Origin = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtcSessionTraffic", x => new { x.SessionId, x.PilotCallsign, x.LegOrdinal });
                    table.ForeignKey(
                        name: "FK_AtcSessionTraffic_AtcSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AtcSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AtcSessions_Callsign_StartUtc",
                table: "AtcSessions",
                columns: new[] { "Callsign", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AtcSessions_ShiftKey",
                table: "AtcSessions",
                column: "ShiftKey");

            migrationBuilder.CreateIndex(
                name: "IX_AtcSessions_StartUtc",
                table: "AtcSessions",
                column: "StartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AtcSessions_UserId_StartUtc",
                table: "AtcSessions",
                columns: new[] { "UserId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AtcSessionTraffic_PilotCallsign",
                table: "AtcSessionTraffic",
                column: "PilotCallsign");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtcSessionTraffic");

            migrationBuilder.DropTable(
                name: "AtcSessions");
        }
    }
}
