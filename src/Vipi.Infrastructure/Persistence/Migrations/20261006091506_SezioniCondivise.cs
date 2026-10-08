using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SezioniCondivise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgreementSectionShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgreementId = table.Column<int>(type: "INTEGER", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementSectionShares");
        }
    }
}
