using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EdizioneMilitare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MilDocumentId",
                table: "Sectors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Edition",
                table: "Documents",
                type: "TEXT",
                nullable: false,
                defaultValue: "Civil");

            migrationBuilder.AddColumn<int>(
                name: "MilDocumentId",
                table: "Airports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_MilDocumentId",
                table: "Sectors",
                column: "MilDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_MilDocumentId",
                table: "Airports",
                column: "MilDocumentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Airports_Documents_MilDocumentId",
                table: "Airports",
                column: "MilDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sectors_Documents_MilDocumentId",
                table: "Sectors",
                column: "MilDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Airports_Documents_MilDocumentId",
                table: "Airports");

            migrationBuilder.DropForeignKey(
                name: "FK_Sectors_Documents_MilDocumentId",
                table: "Sectors");

            migrationBuilder.DropIndex(
                name: "IX_Sectors_MilDocumentId",
                table: "Sectors");

            migrationBuilder.DropIndex(
                name: "IX_Airports_MilDocumentId",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "MilDocumentId",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "Edition",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MilDocumentId",
                table: "Airports");
        }
    }
}
