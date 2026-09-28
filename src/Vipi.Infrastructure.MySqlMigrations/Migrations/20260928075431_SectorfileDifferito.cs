using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
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
                type: "varchar(16)",
                maxLength: 16,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "LatitudeInForce",
                table: "Navaids",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LongitudeInForce",
                table: "Navaids",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAiracCycle",
                table: "Navaids",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "SourceForcePublished",
                table: "Navaids",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MvaChartStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Path = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Text = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TextInForce = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiracCycle = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_uca1400_as_cs")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ForcePublished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MvaChartStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.DropColumn(
                name: "SourceForcePublished",
                table: "Navaids");
        }
    }
}
