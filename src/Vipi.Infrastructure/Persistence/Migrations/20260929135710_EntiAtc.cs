using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AccId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "AtcUnitPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AtcUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    Callsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
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
                });

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
