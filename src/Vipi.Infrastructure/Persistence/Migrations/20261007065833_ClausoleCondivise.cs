using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClauseId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionId = table.Column<int>(type: "INTEGER", nullable: false)
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
                });

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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AgreementId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
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
                });

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
