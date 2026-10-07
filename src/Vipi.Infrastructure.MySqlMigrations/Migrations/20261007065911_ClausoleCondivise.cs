using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ClausoleCondivise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementSectionShares");

            migrationBuilder.CreateTable(
                name: "AgreementClauseShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClauseId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementClauseShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementClauseShares_AgreementClauses_ClauseId",
                        column: x => x.ClauseId,
                        principalTable: "AgreementClauses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgreementClauseShares_AgreementSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "AgreementSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AgreementClauseShares_ClauseId_SectionId",
                table: "AgreementClauseShares",
                columns: new[] { "ClauseId", "SectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgreementClauseShares_SectionId",
                table: "AgreementClauseShares",
                column: "SectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementClauseShares");

            migrationBuilder.CreateTable(
                name: "AgreementSectionShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AgreementId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementSectionShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementSectionShares_AgreementSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "AgreementSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgreementSectionShares_CoordinationAgreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "CoordinationAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AgreementSectionShares_AgreementId_Order",
                table: "AgreementSectionShares",
                columns: new[] { "AgreementId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementSectionShares_SectionId_AgreementId",
                table: "AgreementSectionShares",
                columns: new[] { "SectionId", "AgreementId" },
                unique: true);
        }
    }
}
