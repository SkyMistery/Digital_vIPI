using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpecialAreaCenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chiave alternata su IvaoId: è il riferimento naturale dei legami (l'Id numerico non esce mai da qui).
            migrationBuilder.AddUniqueConstraint(
                name: "AK_SpecialAreas_IvaoId",
                table: "SpecialAreas",
                column: "IvaoId");

            migrationBuilder.CreateTable(
                name: "SpecialAreaCenters",
                columns: table => new
                {
                    IvaoId = table.Column<string>(type: "TEXT", nullable: false),
                    CenterId = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialAreaCenters", x => new { x.IvaoId, x.CenterId });
                    table.ForeignKey(
                        name: "FK_SpecialAreaCenters_Accs_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Accs",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecialAreaCenters_SpecialAreas_IvaoId",
                        column: x => x.IvaoId,
                        principalTable: "SpecialAreas",
                        principalColumn: "IvaoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialAreaCenters_CenterId",
                table: "SpecialAreaCenters",
                column: "CenterId");

            // Backfill PRIMA di lasciare la colonna storica: recupera l'unica appartenenza che il vecchio modello
            // sapeva tenere (le altre le riporta il primo import). L'EXISTS evita di violare la FK se l'ACC non c'è
            // più. In produzione (Postgres, schema allineato dal reconciler e non dalle migration) lo stesso lavoro
            // lo fa ISpecialAreaMaintenance.BackfillAreaCentersAsync al boot.
            migrationBuilder.Sql(@"
                INSERT INTO ""SpecialAreaCenters"" (""IvaoId"", ""CenterId"", ""ImportedAtUtc"")
                SELECT s.""IvaoId"", s.""CenterId"", s.""ImportedAtUtc""
                FROM ""SpecialAreas"" s
                WHERE s.""CenterId"" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM ""Accs"" a WHERE a.""Code"" = s.""CenterId"")");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecialAreas_Accs_CenterId",
                table: "SpecialAreas");

            migrationBuilder.DropIndex(
                name: "IX_SpecialAreas_CenterId",
                table: "SpecialAreas");

            migrationBuilder.DropColumn(
                name: "CenterId",
                table: "SpecialAreas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CenterId",
                table: "SpecialAreas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Ritorno al modello a un solo ente: si tiene un legame per area (il primo per codice), il resto si perde.
            migrationBuilder.Sql(@"
                UPDATE ""SpecialAreas""
                SET ""CenterId"" = COALESCE((
                    SELECT MIN(c.""CenterId"") FROM ""SpecialAreaCenters"" c
                    WHERE c.""IvaoId"" = ""SpecialAreas"".""IvaoId""), '')");

            migrationBuilder.DropTable(
                name: "SpecialAreaCenters");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SpecialAreas_IvaoId",
                table: "SpecialAreas");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialAreas_CenterId",
                table: "SpecialAreas",
                column: "CenterId");

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialAreas_Accs_CenterId",
                table: "SpecialAreas",
                column: "CenterId",
                principalTable: "Accs",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
