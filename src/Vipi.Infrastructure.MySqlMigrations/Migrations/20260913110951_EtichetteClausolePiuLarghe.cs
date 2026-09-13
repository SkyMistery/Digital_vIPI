using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class EtichetteClausolePiuLarghe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "HandoffLabel",
                table: "AgreementClauses",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Cops",
                table: "AgreementClauses",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ConditionCustomLabel",
                table: "AgreementClauses",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ConditionAreaLabel",
                table: "AgreementClauses",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CommsHandoffLabel",
                table: "AgreementClauses",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "HandoffLabel",
                table: "AgreementClauses",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Cops",
                table: "AgreementClauses",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ConditionCustomLabel",
                table: "AgreementClauses",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ConditionAreaLabel",
                table: "AgreementClauses",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CommsHandoffLabel",
                table: "AgreementClauses",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
