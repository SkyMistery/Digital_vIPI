using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AccId = table.Column<int>(type: "int", nullable: false),
                    OwningSectorId = table.Column<int>(type: "int", nullable: false),
                    AirportIcao = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AirportName = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TransferPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FlowId = table.Column<int>(type: "int", nullable: false),
                    NextSectorId = table.Column<int>(type: "int", nullable: true),
                    CommsHandoffKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Unspecified", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CommsHandoffLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConditionAreaLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConditionCustomLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConditionLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConditionRefId = table.Column<int>(type: "int", nullable: true),
                    Cop = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Unspecified", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffLevelConstraint = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "AtOrAbove", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffLevelUnit = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Fl", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffLevelValue = table.Column<int>(type: "int", nullable: true),
                    IsGroupWide = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LevelConstraint = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LevelSpecial = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LevelUnit = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LevelValue = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Parity = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpeedConstraint = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Unspecified", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpeedValue = table.Column<int>(type: "int", nullable: true),
                    VariantDepth = table.Column<int>(type: "int", nullable: false),
                    VariantGroup = table.Column<int>(type: "int", nullable: true),
                    VerticalState = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
