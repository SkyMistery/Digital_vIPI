using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
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
                    SessionId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    ShiftKey = table.Column<long>(type: "INTEGER", nullable: false),
                    TrafficCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MovementCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TrafficMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtcSessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "AtcSessionTraffic",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "INTEGER", nullable: false),
                    PilotCallsign = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LegOrdinal = table.Column<int>(type: "INTEGER", nullable: false),
                    PilotUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    FlightPlanId = table.Column<long>(type: "INTEGER", nullable: true),
                    DepIcao = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    ArrIcao = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    AircraftIcao = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeenMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    SawMovement = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasObservationGap = table.Column<bool>(type: "INTEGER", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false)
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
                });

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
