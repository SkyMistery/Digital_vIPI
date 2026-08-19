using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// **Passo 3 di 3: si toglie il vecchio.** Le colonne nuove diventano <c>NOT NULL</c>, nasce l'indice unico
    /// sulla coppia, e spariscono <c>AgreementParties</c>, <c>TrafficKind</c>, <c>Description</c>,
    /// <c>Direction</c> e le due <c>AgreementId</c>.
    ///
    /// <para>⚠️ <b>Va applicata solo DOPO la conversione</b> (<c>tools/Vipi.AgreementsToSections</c>). Su un
    /// archivio non convertito fallisce, ed è voluto: <c>SideASectorId</c> resterebbe null e il <c>NOT NULL</c>
    /// non passerebbe. Un fallimento rumoroso è l'unica protezione che vale — la trappola pagata a ferragosto è
    /// esattamente quella di una passata che «non trova niente, scrive zero, e i dati spariscono senza un
    /// errore».</para>
    ///
    /// <para>⚠️ Il <c>Down</c> ricrea lo schema, <b>non l'archivio</b>: le parti tornano come tabella vuota e il
    /// tipo di traffico non risale dalle sezioni. Per tornare indietro davvero serve il backup.</para>
    ///
    /// <para>Carta: <c>docs/feature/2026-08-18-accordi-a-sezioni.md</c>.</para>
    /// </summary>
    public partial class AgreementSectionsFinalize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgreementAirports_CoordinationAgreements_AgreementId",
                table: "AgreementAirports");

            migrationBuilder.DropForeignKey(
                name: "FK_AgreementClauses_CoordinationAgreements_AgreementId",
                table: "AgreementClauses");

            migrationBuilder.DropTable(
                name: "AgreementParties");

            migrationBuilder.DropIndex(
                name: "IX_AgreementClauses_AgreementId_Direction_Order",
                table: "AgreementClauses");

            migrationBuilder.DropIndex(
                name: "IX_AgreementClauses_AgreementId_Direction_VariantGroup_Order",
                table: "AgreementClauses");

            migrationBuilder.DropIndex(
                name: "IX_AgreementAirports_AgreementId_Order",
                table: "AgreementAirports");

            migrationBuilder.DropColumn(
                name: "TrafficKind",
                table: "CoordinationAgreements");

            // Description è stata copiata in Note dalla conversione: qui se ne va l'originale.
            migrationBuilder.DropColumn(
                name: "Description",
                table: "CoordinationAgreements");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "AgreementClauses");

            migrationBuilder.DropColumn(
                name: "AgreementId",
                table: "AgreementClauses");

            migrationBuilder.DropColumn(
                name: "AgreementId",
                table: "AgreementAirports");

            // ⚠️ Il NOT NULL è la guardia: su un archivio non convertito queste colonne sono ancora null e la
            // migrazione si ferma qui, rumorosamente, invece di lasciare un archivio a metà.
            migrationBuilder.AlterColumn<int>(
                name: "SideASectorId",
                table: "CoordinationAgreements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SideBSectorId",
                table: "CoordinationAgreements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "AgreementClauses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "AgreementAirports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            // UNA scheda per coppia di enti: i lati stanno in forma canonica (id minore = A) perché in SQL non
            // esiste «insieme di due». ⚠️ Se la conversione non ha fuso i duplicati, questo indice fallisce —
            // ed è il modo giusto di accorgersene.
            migrationBuilder.CreateIndex(
                name: "IX_CoordinationAgreements_SideASectorId_SideBSectorId",
                table: "CoordinationAgreements",
                columns: new[] { "SideASectorId", "SideBSectorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoordinationAgreements_SideBSectorId",
                table: "CoordinationAgreements",
                column: "SideBSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AgreementClauses_SectionId_Order",
                table: "AgreementClauses",
                columns: new[] { "SectionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementClauses_SectionId_VariantGroup_Order",
                table: "AgreementClauses",
                columns: new[] { "SectionId", "VariantGroup", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementAirports_SectionId_Order",
                table: "AgreementAirports",
                columns: new[] { "SectionId", "Order" });

            migrationBuilder.AddForeignKey(
                name: "FK_AgreementAirports_AgreementSections_SectionId",
                table: "AgreementAirports",
                column: "SectionId",
                principalTable: "AgreementSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AgreementClauses_AgreementSections_SectionId",
                table: "AgreementClauses",
                column: "SectionId",
                principalTable: "AgreementSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ⚠️ Restrict e non Cascade: sparire un settore non deve portarsi via l'accordo con tutte le sue
            // sezioni e clausole. Prima spariva solo la PARTE e l'accordo restava monco; adesso il capo è una
            // colonna NOT NULL, quindi il solo modo di non perdere lavoro editoriale è impedire la
            // cancellazione del settore finché un accordo lo cita.
            migrationBuilder.AddForeignKey(
                name: "FK_CoordinationAgreements_Sectors_SideASectorId",
                table: "CoordinationAgreements",
                column: "SideASectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CoordinationAgreements_Sectors_SideBSectorId",
                table: "CoordinationAgreements",
                column: "SideBSectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgreementAirports_AgreementSections_SectionId",
                table: "AgreementAirports");

            migrationBuilder.DropForeignKey(
                name: "FK_AgreementClauses_AgreementSections_SectionId",
                table: "AgreementClauses");

            migrationBuilder.DropForeignKey(
                name: "FK_CoordinationAgreements_Sectors_SideASectorId",
                table: "CoordinationAgreements");

            migrationBuilder.DropForeignKey(
                name: "FK_CoordinationAgreements_Sectors_SideBSectorId",
                table: "CoordinationAgreements");

            migrationBuilder.DropIndex(
                name: "IX_CoordinationAgreements_SideASectorId_SideBSectorId",
                table: "CoordinationAgreements");

            migrationBuilder.DropIndex(
                name: "IX_CoordinationAgreements_SideBSectorId",
                table: "CoordinationAgreements");

            migrationBuilder.DropIndex(
                name: "IX_AgreementClauses_SectionId_Order",
                table: "AgreementClauses");

            migrationBuilder.DropIndex(
                name: "IX_AgreementClauses_SectionId_VariantGroup_Order",
                table: "AgreementClauses");

            migrationBuilder.DropIndex(
                name: "IX_AgreementAirports_SectionId_Order",
                table: "AgreementAirports");

            migrationBuilder.AlterColumn<int>(
                name: "SideASectorId",
                table: "CoordinationAgreements",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "SideBSectorId",
                table: "CoordinationAgreements",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "AgreementClauses",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "AgreementAirports",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "TrafficKind",
                table: "CoordinationAgreements",
                type: "TEXT",
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CoordinationAgreements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "AgreementClauses",
                type: "TEXT",
                nullable: false,
                defaultValue: "AtoB");

            migrationBuilder.AddColumn<int>(
                name: "AgreementId",
                table: "AgreementClauses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AgreementId",
                table: "AgreementAirports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // ⚠️ Torna la tabella, VUOTA: i due capi non risalgono dalle colonne, e il tipo di traffico non
            // risale dalle sezioni. Questo Down rimette lo schema, non l'archivio.
            migrationBuilder.CreateTable(
                name: "AgreementParties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgreementId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementParties_CoordinationAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "CoordinationAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgreementParties_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementClauses_AgreementId_Direction_Order",
                table: "AgreementClauses",
                columns: new[] { "AgreementId", "Direction", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementClauses_AgreementId_Direction_VariantGroup_Order",
                table: "AgreementClauses",
                columns: new[] { "AgreementId", "Direction", "VariantGroup", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementAirports_AgreementId_Order",
                table: "AgreementAirports",
                columns: new[] { "AgreementId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementParties_AgreementId_Side_Order",
                table: "AgreementParties",
                columns: new[] { "AgreementId", "Side", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementParties_SectorId",
                table: "AgreementParties",
                column: "SectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgreementAirports_CoordinationAgreements_AgreementId",
                table: "AgreementAirports",
                column: "AgreementId",
                principalTable: "CoordinationAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AgreementClauses_CoordinationAgreements_AgreementId",
                table: "AgreementClauses",
                column: "AgreementId",
                principalTable: "CoordinationAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
