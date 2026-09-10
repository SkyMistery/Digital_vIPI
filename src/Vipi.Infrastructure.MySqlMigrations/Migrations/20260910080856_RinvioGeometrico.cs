using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class RinvioGeometrico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TargetCallsign",
                table: "SectorFallbacks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TargetKind",
                table: "SectorFallbacks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Callsign",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetKind",
                table: "SectorFallbacks");

            migrationBuilder.UpdateData(
                table: "SectorFallbacks",
                keyColumn: "TargetCallsign",
                keyValue: null,
                column: "TargetCallsign",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "TargetCallsign",
                table: "SectorFallbacks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
