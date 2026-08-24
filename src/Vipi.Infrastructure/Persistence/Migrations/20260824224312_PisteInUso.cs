using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PisteInUso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtcSessionRunways",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<long>(type: "INTEGER", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Arrival = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Departure = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtcSessionRunways", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtcSessionRunways_AtcSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AtcSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AtcSessionRunways_SessionId_FromUtc",
                table: "AtcSessionRunways",
                columns: new[] { "SessionId", "FromUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtcSessionRunways");
        }
    }
}
