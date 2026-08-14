using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropConcurrencyTokensUnused : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UnificationRules",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TransferFlows",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SharedBlocks",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentProfiles",
                type: "BLOB",
                nullable: true);
        }
    }
}
