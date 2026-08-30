using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PezziDiForma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectorShapeParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Catalog = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    PolygonJson = table.Column<string>(type: "TEXT", nullable: false),
                    BaseFeet = table.Column<int>(type: "INTEGER", nullable: true),
                    TopFeet = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseDatum = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TopDatum = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BaseRaw = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TopRaw = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AiracCycle = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    ForcePublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceRef = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    WrittenUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorShapeParts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectorShapeParts_Callsign_State",
                table: "SectorShapeParts",
                columns: new[] { "Callsign", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_SectorShapeParts_Catalog_SectorId_Source_State_Ordinal",
                table: "SectorShapeParts",
                columns: new[] { "Catalog", "SectorId", "Source", "State", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectorShapeParts");
        }
    }
}
