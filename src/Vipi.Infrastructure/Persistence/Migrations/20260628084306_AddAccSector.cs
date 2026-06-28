using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccSector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Accs_Code",
                table: "Accs",
                column: "Code");

            migrationBuilder.CreateTable(
                name: "AccSectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComposePosition = table.Column<string>(type: "TEXT", nullable: false),
                    CenterId = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: true),
                    MiddleIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    Frequency = table.Column<string>(type: "TEXT", nullable: true),
                    RegionMapPolygon = table.Column<string>(type: "TEXT", nullable: true),
                    LowerLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    UpperLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccSectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccSectors_Accs_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Accs",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccSectors_CenterId",
                table: "AccSectors",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_AccSectors_ComposePosition",
                table: "AccSectors",
                column: "ComposePosition",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccSectors");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Accs_Code",
                table: "Accs");
        }
    }
}
