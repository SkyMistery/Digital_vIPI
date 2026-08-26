using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentitaDeiSettori : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IvaoId",
                table: "AirportSectors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IvaoId",
                table: "AccSectors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CallsignAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OldCallsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NewCallsign = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Catalog = table.Column<string>(type: "TEXT", nullable: false),
                    IvaoId = table.Column<int>(type: "INTEGER", nullable: true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    RenamedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallsignAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallsignAliases_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirportSectors_IvaoId",
                table: "AirportSectors",
                column: "IvaoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccSectors_IvaoId",
                table: "AccSectors",
                column: "IvaoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallsignAliases_Catalog_IvaoId",
                table: "CallsignAliases",
                columns: new[] { "Catalog", "IvaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_CallsignAliases_OldCallsign",
                table: "CallsignAliases",
                column: "OldCallsign",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallsignAliases_SectorId",
                table: "CallsignAliases",
                column: "SectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallsignAliases");

            migrationBuilder.DropIndex(
                name: "IX_AirportSectors_IvaoId",
                table: "AirportSectors");

            migrationBuilder.DropIndex(
                name: "IX_AccSectors_IvaoId",
                table: "AccSectors");

            migrationBuilder.DropColumn(
                name: "IvaoId",
                table: "AirportSectors");

            migrationBuilder.DropColumn(
                name: "IvaoId",
                table: "AccSectors");
        }
    }
}
