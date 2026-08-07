using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <summary>
    /// Le tre modifiche di schema delle aree regolamentate (branch <c>feature/aree-speciali-hardening</c>),
    /// emesse per MySQL/MariaDB: categoria di import <c>ImportSpecialAreas</c>, interruttore per ACC
    /// <c>Accs.SpecialAreasEnabled</c> e appartenenza multi-ACC via <c>SpecialAreaCenters</c> (SPEC §9.23).
    /// Sul ramo SQLite sono tre migrazioni separate; qui una sola, perché il set MySQL nasce il 5 agosto 2026
    /// e non ha ancora una storia da rispettare.
    ///
    /// <para>⚠️ <b>L'ordine delle operazioni è stato corretto a mano rispetto allo scaffold.</b> EF aveva messo
    /// il <c>DropColumn</c> di <c>SpecialAreas.CenterId</c> per primo: la colonna storica sarebbe sparita
    /// <i>prima</i> di essere travasata nella nuova tabella, e su un database con dati i legami esistenti si
    /// sarebbero persi in silenzio. Qui si crea prima la tabella, si travasa, e solo dopo si lascia la colonna —
    /// come fa la gemella SQLite <c>20260803210522_SpecialAreaCenters</c>.</para>
    /// </summary>
    public partial class SpecialAreasHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "ImportSids",
                table: "ImportPolicies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AddColumn<bool>(
                name: "ImportSpecialAreas",
                table: "ImportPolicies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SpecialAreasEnabled",
                table: "Accs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SpecialAreas_IvaoId",
                table: "SpecialAreas",
                column: "IvaoId");

            migrationBuilder.CreateTable(
                name: "SpecialAreaCenters",
                columns: table => new
                {
                    IvaoId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CenterId = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialAreaCenters_CenterId",
                table: "SpecialAreaCenters",
                column: "CenterId");

            // Backfill PRIMA di lasciare la colonna storica: recupera l'unica appartenenza che il vecchio modello
            // sapeva tenere (le altre le riporta il primo import). L'EXISTS evita di violare la FK se l'ACC non
            // c'è più. Identificatori fra backtick: è la citazione di MySQL/MariaDB, le virgolette doppie della
            // gemella SQLite qui sarebbero stringhe letterali (fuori da ANSI_QUOTES).
            migrationBuilder.Sql(@"
                INSERT INTO `SpecialAreaCenters` (`IvaoId`, `CenterId`, `ImportedAtUtc`)
                SELECT s.`IvaoId`, s.`CenterId`, s.`ImportedAtUtc`
                FROM `SpecialAreas` s
                WHERE s.`CenterId` IS NOT NULL
                  AND EXISTS (SELECT 1 FROM `Accs` a WHERE a.`Code` = s.`CenterId`)");

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
            migrationBuilder.DropTable(
                name: "SpecialAreaCenters");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SpecialAreas_IvaoId",
                table: "SpecialAreas");

            migrationBuilder.DropColumn(
                name: "ImportSpecialAreas",
                table: "ImportPolicies");

            migrationBuilder.DropColumn(
                name: "SpecialAreasEnabled",
                table: "Accs");

            migrationBuilder.AddColumn<string>(
                name: "CenterId",
                table: "SpecialAreas",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "ImportSids",
                table: "ImportPolicies",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: true);

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
