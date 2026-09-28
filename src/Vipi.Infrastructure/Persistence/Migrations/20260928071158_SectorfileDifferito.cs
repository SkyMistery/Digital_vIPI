using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SectorfileDifferito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FrequencyInForce",
                table: "Navaids",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LatitudeInForce",
                table: "Navaids",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudeInForce",
                table: "Navaids",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAiracCycle",
                table: "Navaids",
                type: "TEXT",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MvaChartStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    TextInForce = table.Column<string>(type: "TEXT", nullable: true),
                    AiracCycle = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MvaChartStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MvaChartStates_Path",
                table: "MvaChartStates",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MvaChartStates");

            migrationBuilder.DropColumn(
                name: "FrequencyInForce",
                table: "Navaids");

            migrationBuilder.DropColumn(
                name: "LatitudeInForce",
                table: "Navaids");

            migrationBuilder.DropColumn(
                name: "LongitudeInForce",
                table: "Navaids");

            migrationBuilder.DropColumn(
                name: "SourceAiracCycle",
                table: "Navaids");
        }
    }
}
