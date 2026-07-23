using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferPointCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionKind",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "None");   // backfill righe esistenti al valore enum None (non "")

            migrationBuilder.AddColumn<string>(
                name: "ConditionLabel",
                table: "TransferPoints",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConditionRefId",
                table: "TransferPoints",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionKind",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "ConditionLabel",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "ConditionRefId",
                table: "TransferPoints");
        }
    }
}
