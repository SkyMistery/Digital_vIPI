using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropVectoringMinimaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VectoringMinimaRows");

            migrationBuilder.DropTable(
                name: "VectoringMinimaSets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VectoringMinimaSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeSectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceAiracCycle = table.Column<string>(type: "TEXT", nullable: false),
                    SourceCommit = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VectoringMinimaSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VectoringMinimaSets_Sectors_ScopeSectorId",
                        column: x => x.ScopeSectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VectoringMinimaRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AreaName = table.Column<string>(type: "TEXT", nullable: false),
                    MinimaFt = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VectoringMinimaRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VectoringMinimaRows_VectoringMinimaSets_SetId",
                        column: x => x.SetId,
                        principalTable: "VectoringMinimaSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VectoringMinimaRows_SetId",
                table: "VectoringMinimaRows",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_VectoringMinimaSets_ScopeSectorId",
                table: "VectoringMinimaSets",
                column: "ScopeSectorId");
        }
    }
}
