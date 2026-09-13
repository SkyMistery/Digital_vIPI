using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChiaviApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Prefisso = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ImprontaSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreataDaUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreataUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevocataUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevocataDaUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UltimoUsoUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiClients_ImprontaSha256",
                table: "ApiClients",
                column: "ImprontaSha256",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiClients");
        }
    }
}
