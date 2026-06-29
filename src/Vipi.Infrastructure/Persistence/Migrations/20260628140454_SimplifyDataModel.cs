using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sectors_SectorGeometries_GeometryId",
                table: "Sectors");

            migrationBuilder.DropTable(
                name: "SectorGeometries");

            migrationBuilder.DropIndex(
                name: "IX_Sectors_GeometryId",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "GeometryId",
                table: "Sectors");

            migrationBuilder.DropColumn(
                name: "ImportAtis",
                table: "ImportPolicies");

            migrationBuilder.DropColumn(
                name: "AtisFrequency",
                table: "Airports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeometryId",
                table: "Sectors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImportAtis",
                table: "ImportPolicies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AtisFrequency",
                table: "Airports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SectorGeometries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourceCallsign = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorGeometries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_GeometryId",
                table: "Sectors",
                column: "GeometryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sectors_SectorGeometries_GeometryId",
                table: "Sectors",
                column: "GeometryId",
                principalTable: "SectorGeometries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
