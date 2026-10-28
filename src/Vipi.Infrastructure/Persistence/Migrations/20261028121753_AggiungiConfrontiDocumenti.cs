using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiConfrontiDocumenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentComparisons",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComputedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EffectiveCycle = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    ChangedVsEffective = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduledCycle = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    ChangedVsScheduled = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduledDerivedChanged = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentComparisons", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentComparisons_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentComparisons");
        }
    }
}
