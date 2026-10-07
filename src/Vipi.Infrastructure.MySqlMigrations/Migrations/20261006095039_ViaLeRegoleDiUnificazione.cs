using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ViaLeRegoleDiUnificazione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnificationRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnificationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AccId = table.Column<int>(type: "int", nullable: false),
                    AssignmentJson = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConditionJson = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnificationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnificationRules_Accs_AccId",
                        column: x => x.AccId,
                        principalTable: "Accs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UnificationRules_AccId_Priority",
                table: "UnificationRules",
                columns: new[] { "AccId", "Priority" });
        }
    }
}
