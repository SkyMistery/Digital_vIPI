using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <summary>
    /// **Passo 1 di 3: solo aggiunte.** Gemella MySQL/MariaDB di
    /// <c>20260818115830_AgreementSectionsAdditive</c> in <c>Vipi.Infrastructure</c>: crea
    /// <c>AgreementSections</c> e le colonne nuove, tutte <b>nullable</b>, senza toccare niente di ciò che c'è.
    ///
    /// <para>⚠️ Fra questa e la <c>AgreementSectionsFinalize</c> deve girare la <b>conversione</b>
    /// (<c>tools/Vipi.AgreementsToSections</c>). È la ragione per cui i passi sono tre: la fusione degli accordi
    /// in coppie è logica — canonizzazione dei lati, ribaltamento dei versi, unione delle gemelle — e scriverla
    /// in SQL due volte, una per dialetto, sarebbe due volte il rischio per lo stesso risultato.</para>
    ///
    /// <para>⚠️ <b>Le migrazioni si leggono, non si accettano.</b> Su quest'area lo scaffolding ha già proposto
    /// un <c>RenameColumn</c> di <c>AgreementId</c> in <c>SectionId</c>: avrebbe lasciato nella colonna nuova
    /// degli <b>id di accordi</b> spacciati per id di sezioni, senza un errore. Qui la colonna nasce nuova e
    /// vuota, e la riempie la conversione.</para>
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AgreementId = table.Column<int>(type: "int", nullable: false),
                    // ⚠️ 32 caratteri e non longtext: sono enum su colonna testuale e stanno in un indice —
                    // una colonna senza lunghezza in un indice MySQL è errno 1170.
                    Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Other", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Direction = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "AtoB", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SideBSectorId",
                table: "CoordinationAgreements",
                type: "int",
                nullable: true);

            // Note affianca Description invece di rinominarla: la conversione ci copia dentro il valore, e
            // finché il passo 3 non gira l'originale resta leggibile.
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "CoordinationAgreements",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "AgreementClauses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "AgreementAirports",
                type: "int",
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
