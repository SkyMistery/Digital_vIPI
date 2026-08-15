using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferHandoffSpeedAndVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommsHandoffKind",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unspecified",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CommsHandoffLabel",
                table: "TransferPoints",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HandoffKind",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unspecified",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HandoffLabel",
                table: "TransferPoints",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HandoffLevelConstraint",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "AtOrAbove",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HandoffLevelUnit",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Fl",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "HandoffLevelValue",
                table: "TransferPoints",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOtherwise",
                table: "TransferPoints",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpeedConstraint",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unspecified",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SpeedValue",
                table: "TransferPoints",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VariantGroup",
                table: "TransferPoints",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup",
                table: "TransferPoints",
                columns: new[] { "FlowId", "VariantGroup" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "CommsHandoffKind",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "CommsHandoffLabel",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffKind",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffLabel",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffLevelConstraint",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffLevelUnit",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffLevelValue",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "IsOtherwise",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "SpeedConstraint",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "SpeedValue",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "VariantGroup",
                table: "TransferPoints");
        }
    }
}
