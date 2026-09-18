using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PonteRfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rfo_shared_state",
                columns: table => new
                {
                    event_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false),
                    data = table.Column<string>(type: "TEXT", nullable: false),
                    updated_by = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfo_shared_state", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "rfo_shared_state_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    event_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false),
                    data = table.Column<string>(type: "TEXT", nullable: false),
                    updated_by = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rfo_shared_state_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rfo_shared_state_history_event_version",
                table: "rfo_shared_state_history",
                columns: new[] { "event_id", "version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rfo_shared_state");

            migrationBuilder.DropTable(
                name: "rfo_shared_state_history");
        }
    }
}
