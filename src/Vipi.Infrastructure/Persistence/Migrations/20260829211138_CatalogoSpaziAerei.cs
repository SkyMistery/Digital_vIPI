using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoSpaziAerei : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AirspaceImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    AiracCycle = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    GeneratedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UploadedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UploadedByName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    VolumesRead = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumesUsable = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicateKeys = table.Column<int>(type: "INTEGER", nullable: false),
                    PointCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IssuesJson = table.Column<string>(type: "TEXT", nullable: true),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirspaceImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AirspaceVolumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportId = table.Column<int>(type: "INTEGER", nullable: false),
                    NaturalKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Family = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AirspaceClass = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    BaseDatum = table.Column<string>(type: "TEXT", nullable: false),
                    BaseFeet = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseRaw = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TopDatum = table.Column<string>(type: "TEXT", nullable: false),
                    TopFeet = table.Column<int>(type: "INTEGER", nullable: true),
                    TopRaw = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PolygonJson = table.Column<string>(type: "TEXT", nullable: false),
                    RingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PointCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MinLat = table.Column<double>(type: "REAL", nullable: false),
                    MinLon = table.Column<double>(type: "REAL", nullable: false),
                    MaxLat = table.Column<double>(type: "REAL", nullable: false),
                    MaxLon = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirspaceVolumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirspaceVolumes_AirspaceImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "AirspaceImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirspaceImports_IsCurrent",
                table: "AirspaceImports",
                column: "IsCurrent");

            migrationBuilder.CreateIndex(
                name: "IX_AirspaceImports_Sha256",
                table: "AirspaceImports",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_AirspaceVolumes_ImportId_Family",
                table: "AirspaceVolumes",
                columns: new[] { "ImportId", "Family" });

            migrationBuilder.CreateIndex(
                name: "IX_AirspaceVolumes_ImportId_NaturalKey_Ordinal",
                table: "AirspaceVolumes",
                columns: new[] { "ImportId", "NaturalKey", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirspaceVolumes");

            migrationBuilder.DropTable(
                name: "AirspaceImports");
        }
    }
}
