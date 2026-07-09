using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNeighbourCandidate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NeighbourCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HomeAccCode = table.Column<string>(type: "TEXT", nullable: false),
                    ForeignAccCode = table.Column<string>(type: "TEXT", nullable: false),
                    ForeignAccName = table.Column<string>(type: "TEXT", nullable: false),
                    CountryId = table.Column<string>(type: "TEXT", nullable: false),
                    ForeignRootCallsign = table.Column<string>(type: "TEXT", nullable: false),
                    RegionMapPolygon = table.Column<string>(type: "TEXT", nullable: true),
                    MinDistanceNm = table.Column<double>(type: "REAL", nullable: true),
                    AdjacentSectorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    VloaDocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeighbourCandidates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NeighbourCandidates_HomeAccCode_ForeignAccCode",
                table: "NeighbourCandidates",
                columns: new[] { "HomeAccCode", "ForeignAccCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NeighbourCandidates");
        }
    }
}
