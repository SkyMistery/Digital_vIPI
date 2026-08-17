using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Droppa <c>TransferFlows</c> e <c>TransferPoints</c>, l'archivio del modello di coordinamento sostituito
    /// dagli accordi il 17 agosto 2026 (carta: <c>docs/feature/2026-08-16-accordi-di-coordinamento.md</c>).
    ///
    /// <para><b>L'ordine del drop non e' cosmetico</b>: <c>TransferPoints</c> ha una FK verso <c>TransferFlows</c>,
    /// quindi il figlio va prima del padre — invertirli e' un errno 150 su MariaDB.</para>
    ///
    /// <para>⚠️ <b>Il <c>Down</c> ricrea le tabelle VUOTE.</b> Non e' un rollback dei dati e non puo' esserlo: i
    /// flussi sono diventati accordi, e la conversione non e' invertibile riga per riga (piu' flussi confluiscono
    /// in un accordo solo, piu' punti in una clausola sola). Serve a far tornare lo schema, non l'archivio. Chi
    /// deve tornare indietro davvero riparte da un backup del database.</para>
    ///
    /// <para>La copia superstite di quei dati nella loro forma originale e' il fixture
    /// <c>tests/Vipi.Application.Tests/Fixtures/real-flows.tsv</c>, che alimenta la rete di caratterizzazione.</para>
    /// </summary>
    public partial class DropLegacyTransferTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferPoints");

            migrationBuilder.DropTable(
                name: "TransferFlows");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransferFlows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwningSectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    AirportIcao = table.Column<string>(type: "TEXT", nullable: true),
                    AirportName = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferFlows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferFlows_Accs_AccId",
                        column: x => x.AccId,
                        principalTable: "Accs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransferFlows_Sectors_OwningSectorId",
                        column: x => x.OwningSectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlowId = table.Column<int>(type: "INTEGER", nullable: false),
                    NextSectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    CommsHandoffKind = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Unspecified"),
                    CommsHandoffLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ConditionAreaLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ConditionCustomLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ConditionLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ConditionRefId = table.Column<int>(type: "INTEGER", nullable: true),
                    Cop = table.Column<string>(type: "TEXT", nullable: false),
                    HandoffKind = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Unspecified"),
                    HandoffLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    HandoffLevelConstraint = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "AtOrAbove"),
                    HandoffLevelUnit = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Fl"),
                    HandoffLevelValue = table.Column<int>(type: "INTEGER", nullable: true),
                    IsGroupWide = table.Column<bool>(type: "INTEGER", nullable: false),
                    LevelConstraint = table.Column<string>(type: "TEXT", nullable: false),
                    LevelSpecial = table.Column<string>(type: "TEXT", nullable: true),
                    LevelUnit = table.Column<string>(type: "TEXT", nullable: false),
                    LevelValue = table.Column<int>(type: "INTEGER", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Parity = table.Column<string>(type: "TEXT", nullable: false),
                    SpeedConstraint = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Unspecified"),
                    SpeedValue = table.Column<int>(type: "INTEGER", nullable: true),
                    VariantDepth = table.Column<int>(type: "INTEGER", nullable: false),
                    VariantGroup = table.Column<int>(type: "INTEGER", nullable: true),
                    VerticalState = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferPoints_Sectors_NextSectorId",
                        column: x => x.NextSectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TransferPoints_TransferFlows_FlowId",
                        column: x => x.FlowId,
                        principalTable: "TransferFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferFlows_AccId_OwningSectorId_Order",
                table: "TransferFlows",
                columns: new[] { "AccId", "OwningSectorId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferFlows_OwningSectorId",
                table: "TransferFlows",
                column: "OwningSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferPoints_FlowId_Order",
                table: "TransferPoints",
                columns: new[] { "FlowId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup_Order",
                table: "TransferPoints",
                columns: new[] { "FlowId", "VariantGroup", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferPoints_NextSectorId",
                table: "TransferPoints",
                column: "NextSectorId");
        }
    }
}
