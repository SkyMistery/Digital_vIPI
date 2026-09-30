using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrezioniSpaziAerei : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AirspaceVolumeCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VolumeKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    VolumeOrdinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Family = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ClassCorrected = table.Column<bool>(type: "INTEGER", nullable: false),
                    AirspaceClass = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    BaseRaw = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    TopRaw = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FileFamily = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FileClass = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    FileBaseRaw = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FileTopRaw = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedByName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirspaceVolumeCorrections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirspaceVolumeCorrections_VolumeKey_VolumeOrdinal",
                table: "AirspaceVolumeCorrections",
                columns: new[] { "VolumeKey", "VolumeOrdinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirspaceVolumeCorrections");
        }
    }
}
