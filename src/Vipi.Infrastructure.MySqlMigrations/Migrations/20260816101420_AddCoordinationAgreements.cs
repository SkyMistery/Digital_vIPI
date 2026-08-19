using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OwnerAccId = table.Column<int>(type: "int", nullable: false),
                    TrafficKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AgreementAirports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AgreementId = table.Column<int>(type: "int", nullable: false),
                    Icao = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AgreementClauses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AgreementId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "AtoB", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cops = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LevelValue = table.Column<int>(type: "int", nullable: true),
                    LevelUnit = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LevelConstraint = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LevelSpecial = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Parity = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VerticalState = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConditionLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConditionRefId = table.Column<int>(type: "int", nullable: true),
                    ConditionAreaLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConditionCustomLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Unspecified", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffLevelValue = table.Column<int>(type: "int", nullable: true),
                    HandoffLevelUnit = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Fl", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HandoffLevelConstraint = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "AtOrAbove", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CommsHandoffKind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Unspecified", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CommsHandoffLabel = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpeedValue = table.Column<int>(type: "int", nullable: true),
                    SpeedConstraint = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Unspecified", collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VariantGroup = table.Column<int>(type: "int", nullable: true),
                    VariantDepth = table.Column<int>(type: "int", nullable: false),
                    IsGroupWide = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AgreementParties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AgreementId = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SectorId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
