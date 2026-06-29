using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReworkTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.CreateTable(
                name: "TransferFlows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwningSectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    AirportIcao = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
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
                    Cop = table.Column<string>(type: "TEXT", nullable: false),
                    LevelValue = table.Column<int>(type: "INTEGER", nullable: true),
                    LevelUnit = table.Column<string>(type: "TEXT", nullable: false),
                    LevelConstraint = table.Column<string>(type: "TEXT", nullable: false),
                    LevelSpecial = table.Column<string>(type: "TEXT", nullable: true),
                    NextSectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    Fallback = table.Column<string>(type: "TEXT", nullable: false),
                    ManualChainJson = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_TransferPoints_NextSectorId",
                table: "TransferPoints",
                column: "NextSectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferPoints");

            migrationBuilder.DropTable(
                name: "TransferFlows");

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccId = table.Column<int>(type: "INTEGER", nullable: false),
                    AirportIcao = table.Column<string>(type: "TEXT", nullable: false),
                    Cop = table.Column<string>(type: "TEXT", nullable: false),
                    FlRule = table.Column<string>(type: "TEXT", nullable: false),
                    HandlerChainJson = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    RelationKey = table.Column<string>(type: "TEXT", nullable: false),
                    RelationLabel = table.Column<string>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    StandardFallback = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transfers_Accs_AccId",
                        column: x => x.AccId,
                        principalTable: "Accs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_AccId_RelationKey_Phase_Order",
                table: "Transfers",
                columns: new[] { "AccId", "RelationKey", "Phase", "Order" });
        }
    }
}
