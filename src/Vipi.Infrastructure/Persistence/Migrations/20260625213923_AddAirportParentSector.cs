using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAirportParentSector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentSectorId",
                table: "Airports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Airports_ParentSectorId",
                table: "Airports",
                column: "ParentSectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Airports_Sectors_ParentSectorId",
                table: "Airports",
                column: "ParentSectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Airports_Sectors_ParentSectorId",
                table: "Airports");

            migrationBuilder.DropIndex(
                name: "IX_Airports_ParentSectorId",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "ParentSectorId",
                table: "Airports");
        }
    }
}
