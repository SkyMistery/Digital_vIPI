using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpecialAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IvaoId = table.Column<string>(type: "TEXT", nullable: false),
                    CenterId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ActivationDetails = table.Column<string>(type: "TEXT", nullable: true),
                    MinimumAlt = table.Column<int>(type: "INTEGER", nullable: true),
                    MaximumAlt = table.Column<int>(type: "INTEGER", nullable: true),
                    Range = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegionMapPolygon = table.Column<string>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecialAreas_Accs_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Accs",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialAreas_CenterId",
                table: "SpecialAreas",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialAreas_IvaoId",
                table: "SpecialAreas",
                column: "IvaoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpecialAreas");
        }
    }
}
