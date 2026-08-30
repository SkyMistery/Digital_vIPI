using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgganciSpaziAerei : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectorAirspaceBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Catalog = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    VolumeKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    VolumeOrdinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedByName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorAirspaceBindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectorAirspaceBindings_Callsign",
                table: "SectorAirspaceBindings",
                column: "Callsign");

            migrationBuilder.CreateIndex(
                name: "IX_SectorAirspaceBindings_Catalog_SectorId_VolumeKey_VolumeOrdinal",
                table: "SectorAirspaceBindings",
                columns: new[] { "Catalog", "SectorId", "VolumeKey", "VolumeOrdinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectorAirspaceBindings");
        }
    }
}
