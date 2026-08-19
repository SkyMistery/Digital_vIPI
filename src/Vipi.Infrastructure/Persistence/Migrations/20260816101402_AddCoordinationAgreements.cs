using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordinationAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoordinationAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerAccId = table.Column<int>(type: "INTEGER", nullable: false),
                    TrafficKind = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoordinationAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoordinationAgreements_Accs_OwnerAccId",
                        column: x => x.OwnerAccId,
                        principalTable: "Accs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgreementAirports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgreementId = table.Column<int>(type: "INTEGER", nullable: false),
                    Icao = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementAirports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementAirports_CoordinationAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "CoordinationAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgreementClauses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgreementId = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "AtoB"),
                    Cops = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LevelValue = table.Column<int>(type: "INTEGER", nullable: true),
                    LevelUnit = table.Column<string>(type: "TEXT", nullable: false),
                    LevelConstraint = table.Column<string>(type: "TEXT", nullable: false),
                    LevelSpecial = table.Column<string>(type: "TEXT", nullable: true),
                    Parity = table.Column<string>(type: "TEXT", nullable: false),
                    VerticalState = table.Column<string>(type: "TEXT", nullable: false),
                    ConditionLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ConditionRefId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConditionAreaLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ConditionCustomLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    HandoffKind = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Unspecified"),
                    HandoffLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    HandoffLevelValue = table.Column<int>(type: "INTEGER", nullable: true),
                    HandoffLevelUnit = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Fl"),
                    HandoffLevelConstraint = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "AtOrAbove"),
                    CommsHandoffKind = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Unspecified"),
                    CommsHandoffLabel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    SpeedValue = table.Column<int>(type: "INTEGER", nullable: true),
                    SpeedConstraint = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Unspecified"),
                    VariantGroup = table.Column<int>(type: "INTEGER", nullable: true),
                    VariantDepth = table.Column<int>(type: "INTEGER", nullable: false),
                    IsGroupWide = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementClauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementClauses_CoordinationAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "CoordinationAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgreementParties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgreementId = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<string>(type: "TEXT", nullable: false),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_AgreementAirports_AgreementId_Order",
                table: "AgreementAirports",
                columns: new[] { "AgreementId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementClauses_AgreementId_Direction_Order",
                table: "AgreementClauses",
                columns: new[] { "AgreementId", "Direction", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementClauses_AgreementId_Direction_VariantGroup_Order",
                table: "AgreementClauses",
                columns: new[] { "AgreementId", "Direction", "VariantGroup", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementParties_AgreementId_Side_Order",
                table: "AgreementParties",
                columns: new[] { "AgreementId", "Side", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementParties_SectorId",
                table: "AgreementParties",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_CoordinationAgreements_OwnerAccId_Order",
                table: "CoordinationAgreements",
                columns: new[] { "OwnerAccId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementAirports");

            migrationBuilder.DropTable(
                name: "AgreementClauses");

            migrationBuilder.DropTable(
                name: "AgreementParties");

            migrationBuilder.DropTable(
                name: "CoordinationAgreements");
        }
    }
}
