using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportTransitionAltitude = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportAtis = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportRunways = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportSectors = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportPolicies", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportPolicies");
        }
    }
}
