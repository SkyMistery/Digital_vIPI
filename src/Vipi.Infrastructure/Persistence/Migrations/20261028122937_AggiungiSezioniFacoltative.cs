using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiSezioniFacoltative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OptionalSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Profile = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SectionKey = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SetUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SetByUserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionalSections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptionalSections_Profile_SectionKey",
                table: "OptionalSections",
                columns: new[] { "Profile", "SectionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OptionalSections");
        }
    }
}
