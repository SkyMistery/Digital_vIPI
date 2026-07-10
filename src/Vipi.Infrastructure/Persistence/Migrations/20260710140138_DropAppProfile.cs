using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropAppProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppFrequencyLinks");

            migrationBuilder.DropTable(
                name: "AppProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    CoordinationSentenceTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    CustomSectionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FreqOrderJson = table.Column<string>(type: "TEXT", nullable: false),
                    HiddenSectionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    SectionOrderJson = table.Column<string>(type: "TEXT", nullable: false),
                    SeparationsJson = table.Column<string>(type: "TEXT", nullable: false),
                    VfrJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppProfiles_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppFrequencyLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceSectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    LabelOverride = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppFrequencyLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppFrequencyLinks_AppProfiles_AppProfileId",
                        column: x => x.AppProfileId,
                        principalTable: "AppProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppFrequencyLinks_Sectors_SourceSectorId",
                        column: x => x.SourceSectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppFrequencyLinks_AppProfileId_Order",
                table: "AppFrequencyLinks",
                columns: new[] { "AppProfileId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AppFrequencyLinks_SourceSectorId",
                table: "AppFrequencyLinks",
                column: "SourceSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppProfiles_SectorId",
                table: "AppProfiles",
                column: "SectorId",
                unique: true);
        }
    }
}
