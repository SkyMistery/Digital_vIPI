using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<byte[]>(type: "longblob", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AiracCycle = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UploadedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: true),
                    UploadedByName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VolumesRead = table.Column<int>(type: "int", nullable: false),
                    VolumesUsable = table.Column<int>(type: "int", nullable: false),
                    DuplicateKeys = table.Column<int>(type: "int", nullable: false),
                    PointCount = table.Column<int>(type: "int", nullable: false),
                    IssuesJson = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirspaceImports", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AirspaceVolumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ImportId = table.Column<int>(type: "int", nullable: false),
                    NaturalKey = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Family = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AirspaceClass = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseDatum = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseFeet = table.Column<int>(type: "int", nullable: true),
                    BaseRaw = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TopDatum = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TopFeet = table.Column<int>(type: "int", nullable: true),
                    TopRaw = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PolygonJson = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RingCount = table.Column<int>(type: "int", nullable: false),
                    PointCount = table.Column<int>(type: "int", nullable: false),
                    MinLat = table.Column<double>(type: "double", nullable: false),
                    MinLon = table.Column<double>(type: "double", nullable: false),
                    MaxLat = table.Column<double>(type: "double", nullable: false),
                    MaxLon = table.Column<double>(type: "double", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
