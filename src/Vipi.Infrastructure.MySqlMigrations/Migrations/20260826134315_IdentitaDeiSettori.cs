using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IvaoId",
                table: "AccSectors",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CallsignAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OldCallsign = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewCallsign = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Catalog = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IvaoId = table.Column<int>(type: "int", nullable: true),
                    SectorId = table.Column<int>(type: "int", nullable: true),
                    RenamedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
