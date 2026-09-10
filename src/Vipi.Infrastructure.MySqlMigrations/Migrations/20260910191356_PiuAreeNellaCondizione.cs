using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class PiuAreeNellaCondizione : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ConditionAreaLabel",
                table: "AgreementClauses",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "ConditionAreaAll",
                table: "AgreementClauses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionAreaAll",
                table: "AgreementClauses");

            migrationBuilder.AlterColumn<string>(
                name: "ConditionAreaLabel",
                table: "AgreementClauses",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
