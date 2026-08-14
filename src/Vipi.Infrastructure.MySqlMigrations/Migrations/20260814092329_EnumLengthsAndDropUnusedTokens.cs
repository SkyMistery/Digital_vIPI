using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class EnumLengthsAndDropUnusedTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UnificationRules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TransferFlows");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SharedBlocks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DocumentProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "VerticalState",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Parity",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelUnit",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelConstraint",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "TransferFlows",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                table: "SharedBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Sectors",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Sectors",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ApproachKind",
                table: "Sectors",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NeighbourCandidates",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TargetType",
                table: "EditorTasks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "EditorTasks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocumentVersions",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "Documents",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "DocumentParties",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocReleases",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "CoordinationPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Visibility",
                table: "ContentBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Tier",
                table: "ContentBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                table: "ContentBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CalloutKind",
                table: "ContentBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Surface",
                table: "AirportRunwayRules",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DateParity",
                table: "AirportRunwayRules",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UnificationRules",
                type: "longblob",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VerticalState",
                table: "TransferPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Parity",
                table: "TransferPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelUnit",
                table: "TransferPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelConstraint",
                table: "TransferPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "TransferFlows",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TransferFlows",
                type: "longblob",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                table: "SharedBlocks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SharedBlocks",
                type: "longblob",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Sectors",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Sectors",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ApproachKind",
                table: "Sectors",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NeighbourCandidates",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TargetType",
                table: "EditorTasks",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "EditorTasks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocumentVersions",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "Documents",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentProfiles",
                type: "longblob",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "DocumentParties",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocReleases",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "CoordinationPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Visibility",
                table: "ContentBlocks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Tier",
                table: "ContentBlocks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                table: "ContentBlocks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CalloutKind",
                table: "ContentBlocks",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Surface",
                table: "AirportRunwayRules",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DateParity",
                table: "AirportRunwayRules",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
