using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RadioassistenzeAnagrafica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImportNavaids",
                table: "ImportPolicies",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Navaids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DisplayType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    FrequencyOrigin = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelOrigin = table.Column<string>(type: "TEXT", nullable: false),
                    CoordinatesOrigin = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Navaids", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Navaids_Code_Kind",
                table: "Navaids",
                columns: new[] { "Code", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Navaids");

            migrationBuilder.DropColumn(
                name: "ImportNavaids",
                table: "ImportPolicies");
        }
    }
}
