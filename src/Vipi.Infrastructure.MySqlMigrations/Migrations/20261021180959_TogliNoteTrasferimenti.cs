using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <summary>
    /// Via la nota libera della coppia (<c>CoordinationAgreements.Note</c>) e la descrizione della sezione
    /// (<c>AgreementSections.Description</c>) dei trasferimenti (S123c, 21 ottobre 2026, committente: «togli le note e le
    /// descrizioni dei trasferimenti senza portarle nei documenti»). Non erano mai uscite in un documento.
    /// ⚠️ Distruttiva, e il testo si perde apposta: sulla copia di produzione del 21 ottobre c'erano 0 note e 5
    /// descrizioni (elenco in <c>docs/feature/2026-10-21-via-le-note.md</c> §4); le due frasi di Milano il committente le
    /// riscrive dove servono.
    /// </summary>
    public partial class TogliNoteTrasferimenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "CoordinationAgreements");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AgreementSections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "CoordinationAgreements",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AgreementSections",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
