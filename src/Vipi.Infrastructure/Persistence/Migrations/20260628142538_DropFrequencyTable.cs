using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropFrequencyTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AirportFrequencyLinks_Frequencies_SourceFrequencyId",
                table: "AirportFrequencyLinks");

            migrationBuilder.DropTable(
                name: "Frequencies");

            migrationBuilder.RenameColumn(
                name: "SourceFrequencyId",
                table: "AirportFrequencyLinks",
                newName: "SourceSectorId");

            migrationBuilder.RenameIndex(
                name: "IX_AirportFrequencyLinks_SourceFrequencyId",
                table: "AirportFrequencyLinks",
                newName: "IX_AirportFrequencyLinks_SourceSectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_AirportFrequencyLinks_Sectors_SourceSectorId",
                table: "AirportFrequencyLinks",
                column: "SourceSectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AirportFrequencyLinks_Sectors_SourceSectorId",
                table: "AirportFrequencyLinks");

            migrationBuilder.RenameColumn(
                name: "SourceSectorId",
                table: "AirportFrequencyLinks",
                newName: "SourceFrequencyId");

            migrationBuilder.RenameIndex(
                name: "IX_AirportFrequencyLinks_SourceSectorId",
                table: "AirportFrequencyLinks",
                newName: "IX_AirportFrequencyLinks_SourceFrequencyId");

            migrationBuilder.CreateTable(
                name: "Frequencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Callsign = table.Column<string>(type: "TEXT", nullable: false),
                    FrequencyMhz = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Frequencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Frequencies_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Frequencies_SectorId",
                table: "Frequencies",
                column: "SectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_AirportFrequencyLinks_Frequencies_SourceFrequencyId",
                table: "AirportFrequencyLinks",
                column: "SourceFrequencyId",
                principalTable: "Frequencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
