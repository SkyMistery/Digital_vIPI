using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class EntiAtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtcUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccId = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtcUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtcUnits_Accs_AccId",
                        column: x => x.AccId,
                        principalTable: "Accs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtcUnits_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AtcUnitPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AtcUnitId = table.Column<int>(type: "int", nullable: false),
                    Callsign = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtcUnitPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtcUnitPositions_AtcUnits_AtcUnitId",
                        column: x => x.AtcUnitId,
                        principalTable: "AtcUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AtcUnitPositions_AtcUnitId",
                table: "AtcUnitPositions",
                column: "AtcUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AtcUnitPositions_Callsign",
                table: "AtcUnitPositions",
                column: "Callsign",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtcUnits_AccId",
                table: "AtcUnits",
                column: "AccId");

            migrationBuilder.CreateIndex(
                name: "IX_AtcUnits_Code",
                table: "AtcUnits",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtcUnits_DocumentId",
                table: "AtcUnits",
                column: "DocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtcUnitPositions");

            migrationBuilder.DropTable(
                name: "AtcUnits");
        }
    }
}
