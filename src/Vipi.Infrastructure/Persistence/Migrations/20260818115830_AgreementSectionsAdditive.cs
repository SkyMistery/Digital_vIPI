using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// **Passo 1 di 3: solo aggiunte.** Crea <c>AgreementSections</c> e le colonne nuove, tutte
    /// <b>nullable</b>, e non tocca niente di ciò che c'è: <c>TrafficKind</c>, <c>Description</c>,
    /// <c>Direction</c>, <c>AgreementParties</c> e le due <c>AgreementId</c> restano dove sono.
    ///
    /// <para>⚠️ <b>Perché in tre passi e non in uno.</b> Fra questa e la
    /// <c>AgreementSectionsFinalize</c> deve girare la <b>conversione</b>
    /// (<c>tools/Vipi.AgreementsToSections</c>): quaranta accordi diventano diciassette, i versi si ribaltano
    /// dove i lati si scambiano e le sezioni gemelle si uniscono. È logica, non SQL — scriverla due volte in due
    /// dialetti sarebbe due volte il rischio per lo stesso risultato, su un archivio che non si può rifare.</para>
    ///
    /// <para>⚠️ Una migrazione che droppasse e una passata che leggesse la stessa tabella nella stessa release
    /// perderebbero i dati <b>senza un errore</b> — le migrazioni girano prima della manutenzione d'avvio
    /// (<c>Vipi.Host/Program.cs</c>). Questa non droppa niente, quindi applicarla da sola è sempre innocuo.</para>
    ///
    /// <para>Carta: <c>docs/feature/2026-08-18-accordi-a-sezioni.md</c>.</para>
    /// </summary>
    public partial class AgreementSectionsAdditive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgreementSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgreementId = table.Column<int>(type: "INTEGER", nullable: false),
                    // Enum su colonna testuale (conversione globale nel context): il default è lo zero
                    // dell'enum, e serve al PostgresSchemaReconciler oltre che a questa migrazione.
                    Kind = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Other"),
                    Direction = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "AtoB"),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementSections_CoordinationAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "CoordinationAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementSections_AgreementId_Order",
                table: "AgreementSections",
                columns: new[] { "AgreementId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementSections_AgreementId_Kind_Direction",
                table: "AgreementSections",
                columns: new[] { "AgreementId", "Kind", "Direction" });

            // I due capi: NULLABLE qui, NOT NULL nel passo 3. Fra i due, la conversione li riempie in forma
            // canonica (id minore = A) leggendoli da AgreementParties.
            migrationBuilder.AddColumn<int>(
                name: "SideASectorId",
                table: "CoordinationAgreements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SideBSectorId",
                table: "CoordinationAgreements",
                type: "INTEGER",
                nullable: true);

            // Note affianca Description invece di rinominarla: la conversione ci copia dentro il valore, e
            // finché il passo 3 non gira l'originale resta leggibile.
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "CoordinationAgreements",
                type: "TEXT",
                nullable: true);

            // Il nuovo padre di clausole e aeroporti. Nullable finché la conversione non lo riempie: senza
            // questa colonna la conversione non avrebbe dove scrivere, e con un RENAME della vecchia si
            // ritroverebbe id di ACCORDI spacciati per id di sezioni.
            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "AgreementClauses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "AgreementAirports",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SectionId", table: "AgreementAirports");
            migrationBuilder.DropColumn(name: "SectionId", table: "AgreementClauses");
            migrationBuilder.DropColumn(name: "Note", table: "CoordinationAgreements");
            migrationBuilder.DropColumn(name: "SideBSectorId", table: "CoordinationAgreements");
            migrationBuilder.DropColumn(name: "SideASectorId", table: "CoordinationAgreements");
            migrationBuilder.DropTable(name: "AgreementSections");
        }
    }
}
