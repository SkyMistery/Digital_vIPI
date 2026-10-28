using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ComputedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EffectiveCycle = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedVsEffective = table.Column<int>(type: "int", nullable: false),
                    ScheduledCycle = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChangedVsScheduled = table.Column<int>(type: "int", nullable: false),
                    ScheduledDerivedChanged = table.Column<bool>(type: "tinyint(1)", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentComparisons");
        }
    }
}
