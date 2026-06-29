using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAirportSector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Airports_Icao",
                table: "Airports",
                column: "Icao");

            migrationBuilder.CreateTable(
                name: "AirportSectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComposePosition = table.Column<string>(type: "TEXT", nullable: false),
                    AirportIcao = table.Column<string>(type: "TEXT", nullable: false),
                    AccCode = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: true),
                    MiddleIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    Frequency = table.Column<string>(type: "TEXT", nullable: true),
                    RegionMapPolygon = table.Column<string>(type: "TEXT", nullable: true),
                    LowerLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    UpperLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportSectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirportSectors_Accs_AccCode",
                        column: x => x.AccCode,
                        principalTable: "Accs",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AirportSectors_Airports_AirportIcao",
                        column: x => x.AirportIcao,
                        principalTable: "Airports",
                        principalColumn: "Icao",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirportSectors_AccCode",
                table: "AirportSectors",
                column: "AccCode");

            migrationBuilder.CreateIndex(
                name: "IX_AirportSectors_AirportIcao",
                table: "AirportSectors",
                column: "AirportIcao");

            migrationBuilder.CreateIndex(
                name: "IX_AirportSectors_ComposePosition",
                table: "AirportSectors",
                column: "ComposePosition",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirportSectors");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Airports_Icao",
                table: "Airports");
        }
    }
}
